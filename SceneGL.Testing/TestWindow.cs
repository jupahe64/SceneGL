using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Silk.NET.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ImGuiNET;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Maths;
using SceneGL.Testing.Util;
using SceneGL.GLWrappers;
using Framebuffer = SceneGL.GLWrappers.Framebuffer;
using SixLabors.ImageSharp.ColorSpaces;
using SceneGL.GLHelpers;
using SceneGL.Materials;
using EditTK;

namespace SceneGL.Testing
{
    internal class TestWindow
    {
        private static readonly object _glLock = new();

        private readonly IWindow _window;
        private GL? _gl;
        private IInputContext? _input;
        private ImGuiController? _imguiController;
        private ITransformAction? _transformAction;

        private ShaderSource _vertexShaderSource = InfiniteGrid.VertexSource;
        private ShaderSource _fragmentShaderSource = InfiniteGrid.FragmentSource;
        private Dictionary<ShaderSource, string> _shaderCodeErrors = new();
        private Dictionary<(ShaderSource vert, ShaderSource frag), string> _shaderLinkingErrors = new();

        private Vector4 _color = Vector4.Zero;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _viewProjection = Matrix4x4.Identity;
        private Vector3 _transform_pos = Vector3.Zero;
        private float _transform_yaw = 0;
        private float _transform_pitch = 0;
        private float _transform_roll = 0;
        private Vector3 _transform_scale = new Vector3(1f, 0.7f, 1);

        private Camera _camera = new Camera(new Vector3(-10, 7, 10), Vector3.Zero);

        private Framebuffer? _sceneFB;
        private bool _initialLayoutSetup = false;
        private DockSpace? _mainDock;
        private bool _isSceneHoveredBeforeDrag;

        private List<Instances.InstanceData> _instanceData;
        private readonly List<Gizmos.InstanceData> _gizmoPositions;
        private CombinerMaterial? _material;

        public TestWindow()
        {
            var options = WindowOptions.Default;

            options.VSync = false;

            options.SharedContext = WindowManager.SharedContext;

            options.API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.Debug | ContextFlags.ForwardCompatible,
                new APIVersion(3,3)
                );

            _window = Window.Create(options);

            

            _window.Load += () =>
            {
                _gl = _window.CreateOpenGL();
                _input = _window.CreateInput();

                _window.Title = $"SceneGL.Testing OpenGL {_gl.GetStringS(StringName.Version)} {_gl.GetStringS(StringName.Vendor)}";




                lock (_glLock)
                {
                    _imguiController = new ImGuiController(_gl, _window, _input);
                }

                var win32 = _window.Native!.Win32;

                if(win32 is not null)
                {
                    var (hWnd, _, _) = win32.Value;
                    WindowsDarkmodeUtil.SetDarkmodeAware(hWnd);
                }
                

                //prevent windows from freezing when resizing/moving
                _window.Resize += (size) =>
                {
                    _window.DoUpdate();
                    _window.DoRender();
                };

                _window.Move += (size) =>
                {
                    _window.DoUpdate();
                    _window.DoRender();
                };

                Init();
            };

            _window.Update += Update;
            _window.Render += Render;



            _instanceData = new();

            //for (int i = 0; i < 300000; i+=3)
            //{
            //    int z = i/3;
            //    //x = 0;
            //    _instanceData[i] = new(Matrix4x4.CreateTranslation(0, 0, -z));
            //    _instanceData[i + 1] = new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(-2, 0, -z));
            //    _instanceData[i + 2] = new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(2, 0, -z));
            //}

            var rng = new Random(0);

            //for (int i = 0; i < 300000; i++)
            //{
            //    _instanceData.Add(new(Matrix4x4.CreateScale(rng.Next(5,20)/10f) * 
            //        Matrix4x4.CreateTranslation(
            //            rng.Next(-100, 100)*10, 
            //            rng.Next(-100, 100)*10,
            //            rng.Next(-100, 100)*10)));
            //}

            void Box(int left, int top, int right, int bottom, int height)
            {
                for (int x = left; x < right; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        for (int z = top; z < bottom; z++)
                        {
                            _instanceData.Add(new(
                                Matrix4x4.CreateTranslation(x+0.5f, y+0.5f, z+0.5f)
                            ));
                        }

                    }
                }
            }

            //hardcoded level design cause yes

            Box(-3, -3, 7, 3, 1);

            Box(9, -3, 13, 3, 3);

            Box(13, -3, 19, 3, 5);

            Box(13, -20, 19, -12, 5);

            Box(22, -20, 25, -17, 6);

            Box(24, -16, 27, -13, 8);

            Box(29, -18, 31, -16, 11);

            Box(32, -20, 40, -12, 12);

            Box(38, -6, 40, 4, 12);

            Box(38, 8, 44, 14, 12);


            _gizmoPositions = new List<Gizmos.InstanceData>
            {
                new Gizmos.InstanceData
                {
                    Position=new Vector3(0, 5, 0), 
                    Color=new Vector3(1, 0.5f, 0.5f) 
                },
                new Gizmos.InstanceData
                {
                    Position=new Vector3(15, 9, -16), 
                    Color=new Vector3(1, 1, 0.8f) 
                },
                new Gizmos.InstanceData
                {
                    Position=new Vector3(41, 18, 13), 
                    Color=new Vector3(0.5f, 1.0f, 1.0f) 
                },
            };

            _transform = Matrix4x4.CreateTranslation(0, 3, 0);
        }

        public void AddToWindowManager() => _window.AddToWindowManager();


        private void Init()
        {
            _mainDock = new DockSpace("MainDockSpace");

            Debug.Assert(_gl != null);

            bool isDebugLogSupported = _gl.IsExtensionPresent("GL_ARB_debug_output");

            if (isDebugLogSupported)
            {
                _gl.Enable(EnableCap.DebugOutput);
                _gl.DebugMessageCallback(
                (source, type, id, severity, length, message, _) =>
                {

                    string? messageContent = Marshal.PtrToStringAnsi(new IntPtr(message), length);

                    Debug.WriteLine(messageContent);

                    //if (severity != GLEnum.DebugSeverityNotification)
                    //    Debugger.Break();
                },
                ReadOnlySpan<byte>.Empty);
            }

            _gl.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            _gl.ClearDepth(0);

            _gl.Enable(EnableCap.CullFace);
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Gequal);

            _sceneFB = new Framebuffer(null, InternalFormat.DepthStencil, InternalFormat.Rgb);

            ColoredTriangle.Initialize(_gl);
            Instances.Initialize(_gl);
            InfiniteGrid.Initialize(_gl);
            Gizmos.Initialize(_gl);

            _material = Instances.CreateMaterial(_gl, _color);

            if (isDebugLogSupported)
            {
                //prevent Buffer info message from spamming the debug console
                _gl.DebugMessageControl(DebugSource.DebugSourceApi, DebugType.DebugTypeOther, DebugSeverity.DontCare,
                    stackalloc[] { (uint)131185 }, false);
            }
        }

        private void Update(double deltaSeconds)
        {
            Debug.Assert(_input != null);
            Debug.Assert(_imguiController != null);

            _imguiController.Update((float)deltaSeconds);
        }

        private void UpdateCamera(bool sceneViewHovered, Camera camera, double deltaSeconds,
            out Vector3 eyeAnimated, out Quaternion rotAnimated, out Matrix4x4 viewMatrix)
        {
            if (sceneViewHovered)
            {
                if (ImGui.IsMouseDragging(ImGuiMouseButton.Right))
                {
                    Vector2 delta = ImGui.GetIO().MouseDelta;

                    Vector3 right = Vector3.Transform(Vector3.UnitX, camera.Rotation);
                    camera.Rotation = Quaternion.CreateFromAxisAngle(right, -delta.Y * 0.002f) * camera.Rotation;

                    camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -delta.X * 0.002f) * camera.Rotation;
                }

                float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);

                var keyboard = _input!.Keyboards[0];

                if (keyboard.IsKeyPressed(Key.W))
                    camera.Eye -= Vector3.Transform(Vector3.UnitZ * camMoveSpeed, camera.Rotation);
                if (keyboard.IsKeyPressed(Key.S))
                    camera.Eye += Vector3.Transform(Vector3.UnitZ * camMoveSpeed, camera.Rotation);

                if (keyboard.IsKeyPressed(Key.A))
                    camera.Eye -= Vector3.Transform(Vector3.UnitX * camMoveSpeed, camera.Rotation);
                if (keyboard.IsKeyPressed(Key.D))
                    camera.Eye += Vector3.Transform(Vector3.UnitX * camMoveSpeed, camera.Rotation);

                if (keyboard.IsKeyPressed(Key.Q))
                    camera.Eye -= Vector3.UnitY * camMoveSpeed;
                if (keyboard.IsKeyPressed(Key.E))
                    camera.Eye += Vector3.UnitY * camMoveSpeed;
            }

            _camera.Animate(deltaSeconds, out eyeAnimated, out rotAnimated);

            viewMatrix =
                Matrix4x4.CreateTranslation(-eyeAnimated) *
                Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated));
        }

        private void Render(double deltaSeconds)
        {
            Debug.Assert(_gl != null);
            Debug.Assert(_imguiController != null);
            Debug.Assert(_input != null);
            Debug.Assert(_mainDock != null);
            Debug.Assert(_sceneFB != null);


            //deltaSeconds = Math.Min(deltaSeconds, 1 / 60f);

            _imguiController.MakeCurrent();
            
            var mousePos = ImGui.GetMousePos();

            var viewport = ImGui.GetMainViewport();

            

            if (!_initialLayoutSetup)
            {
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;

                _mainDock.Setup(new DockLayout(
                    ImGuiDir.Left, 0.8f,
                    new DockLayout("Scene View"),
                    new DockLayout(
                        ImGuiDir.Up, 0.3f,
                        new DockLayout("Dear ImGui Demo"),
                        new DockLayout("Test Panel")
                    )
                ));

                //_initialLayoutSetup = true;
            }

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

            ImGui.SetNextWindowPos(viewport.Pos);
            ImGui.SetNextWindowSize(viewport.Size);
            ImGui.SetNextWindowViewport(viewport.ID);

            ImGui.Begin("MainDockSpace", ImGuiWindowFlags.NoDecoration | 
                ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.MenuBar);
            ImGui.PopStyleVar(2);

            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    ImGui.MenuItem("New");
                    ImGui.MenuItem("Open");
                    ImGui.MenuItem("Save");
                    ImGui.MenuItem("Save as");

                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }
            ImGui.DockSpace(_mainDock.DockId);
            ImGui.End();





            ImGui.ShowDemoWindow();



            //Window: Scene View

            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(0));
            ImGui.Begin("Scene View");

            
            Vector2 sizeAvail = ImGui.GetContentRegionAvail();

            bool isSceneHovered = false;

            bool isRightDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Right);

            if (sizeAvail.X > 0 && sizeAvail.Y > 0)
            {
                _sceneFB.SetSize((uint)sizeAvail.X, (uint)sizeAvail.Y);
                _sceneFB.Create(_gl);

                var topLeft = ImGui.GetCursorPos();

                var size = new Vector2(sizeAvail.X, sizeAvail.Y);

                ImGui.Image(new IntPtr(_sceneFB.GetColorTexture(0)), size,
                    new Vector2(0, 1), new Vector2(1, 0));

                float aspectRatio = sizeAvail.X / sizeAvail.Y;

                if (ImGui.IsItemHovered())
                {
                    isSceneHovered = true;
                }


                UpdateCamera(isSceneHovered || _isSceneHoveredBeforeDrag,
                    _camera, deltaSeconds, out Vector3 eyeAnimated, out Quaternion rotAnimated, out var viewMatrix);

                float fov = 1;

                _viewProjection =
                    viewMatrix *
                    NumericsUtil.CreatePerspectiveReversedDepth(fov, aspectRatio, 0.1f);

                CameraState cameraState = new(
                        eyeAnimated,
                        Vector3.Transform(-Vector3.UnitZ, rotAnimated),
                        Vector3.Transform(Vector3.UnitY, rotAnimated),
                        rotAnimated);


                float yScale = 1.0f / (float)Math.Tan(fov * 0.5f);
                float xScale = yScale / aspectRatio;

                Vector2 ndcMousePos = ((mousePos-topLeft) / size * 2 - Vector2.One) * new Vector2(1, -1);

                Vector3 mouseRayDirection = Vector3.Transform(
                    Vector3.Normalize(new(
                        ndcMousePos.X / xScale,
                        ndcMousePos.Y / yScale,
                        -1
                    )) , rotAnimated);

                GizmoDrawer.BeginGizmoDrawing("scene_gizmos", ImGui.GetWindowDrawList(), _viewProjection,
                    new Rect(topLeft, topLeft + size), cameraState);

                if (_transformAction != null)
                {
                    var actionRes = _transformAction.Update(in cameraState, in mouseRayDirection, ImGui.IsKeyDown(ImGuiKey.ModShift));

                    _transform = _transformAction.FinalMatrix;

                    if (actionRes == ActionUpdateResult.Apply)
                    {
                        _transform = _transformAction.FinalMatrix;
                        _transformAction = null;
                    }
                    else if(actionRes == ActionUpdateResult.Cancel)
                    {
                        //just for demonstration purposes, this is NOT the correct way to do this
                        Debug.Assert(Matrix4x4.Invert(_transformAction.DeltaMatrix, out var inv));
                        _transform *= inv;

                        _transformAction = null;
                    }
                }
                else
                {
                    //if (GizmoDrawer.RotationGizmo(_transform, 80, out var hoveredAxis) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    //{
                    //    const double AngleSnapping = 45;

                    //    if (GizmoResultHelper.IsSingleAxis(hoveredAxis, out int axis))
                    //        _transformAction = AxisRotationAction.Start(axis,
                    //            Vector3.Transform(new(), _transform), _transform, AngleSnapping, 
                    //            in cameraState, in mouseRayDirection);

                    //    else if(hoveredAxis == HoveredAxis.VIEW_AXIS)
                    //        _transformAction = AxisRotationAction.StartViewAxisRotation(
                    //            Vector3.Transform(new(), _transform), _transform, AngleSnapping, 
                    //            in cameraState, in mouseRayDirection);

                    //    else if(hoveredAxis == HoveredAxis.TRACKBALL)
                    //        _transformAction = TrackballRotationAction.Start(
                    //            Vector3.Transform(new(), _transform), _transform, AngleSnapping, 
                    //            in cameraState, in mouseRayDirection);
                    //}

                    if (GizmoDrawer.TranslationGizmo(_transform, 80, out var hoveredAxis) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        const float UnitSnapping = 0.5f;

                        if (GizmoResultHelper.IsSingleAxis(hoveredAxis, out int axis))
                            _transformAction = AxisTranslateAction.Start(axis,
                                Vector3.Transform(new(), _transform), _transform, UnitSnapping,
                                in cameraState, in mouseRayDirection);

                        else if (GizmoResultHelper.IsPlane(hoveredAxis, out int axisA, out int axisB))
                            _transformAction = PlaneTranslateAction.Start(axisA, axisB,
                                Vector3.Transform(new(), _transform), _transform, UnitSnapping,
                                in cameraState, in mouseRayDirection);

                        else if (hoveredAxis == HoveredAxis.FREE)
                            _transformAction = FreeTranslateAction.Start(
                                Vector3.Transform(new(), _transform), _transform, UnitSnapping,
                                in cameraState, in mouseRayDirection);
                    }
                }


                GizmoDrawer.OrientationCube(new Vector2(100, 100), 40, out _);

                GizmoDrawer.EndGizmoDrawing();
            }

            if (!isRightDragging)
            {
                _isSceneHoveredBeforeDrag = isSceneHovered;
            }

            if(isSceneHovered || _isSceneHoveredBeforeDrag)
            {
                ImGui.GetIO().ClearInputKeys();
                ImGui.GetIO().ClearInputCharacters();
            }
            
            
            ImGui.End();
            ImGui.PopStyleVar();





            //Window: Test Panel

            ImGui.Begin("Test Panel");

            if (ImGui.Button("Add window"))
            {
                new TestWindow().AddToWindowManager();
            }

            if (ImGui.Button("Reset Camera"))
            {
                _camera.LookAt(new Vector3(-10, 7, 10), Vector3.Zero);
            }

            _gl.GetInteger(GetPName.MaxUniformBlockSize, out int res);
            ImGui.Text($"MaxUniformBlockSize: {res}");

            _gl.GetInteger(GetPName.MaxTextureImageUnits, out res);
            ImGui.Text($"MaxTextureImageUnits: {res}");

            _gl.GetInteger(GetPName.MaxTextureBufferSize, out res);
            ImGui.Text($"MaxTextureBufferSize: {res}");

            _gl.GetInteger(GetPName.MaxVertexUniformBlocks, out res);
            ImGui.Text($"MaxVertexUniformBlocks: {res}");

            _gl.GetInteger(GetPName.MaxFragmentUniformBlocks, out res);
            ImGui.Text($"MaxFragmentUniformBlocks: {res}");

            _gl.GetInteger(GetPName.UniformBufferOffsetAlignment, out res);
            ImGui.Text($"UniformBufferOffsetAlignment: {res}");

            ImGui.PushStyleColor(ImGuiCol.Header, 0x0);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x33_33_33_33);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
            if (ImGui.CollapsingHeader("Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat3("Position", ref _transform_pos, 0.01f);

                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Yaw", ref _transform_yaw);
                ImGui.SetKeyboardFocusHere();
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Pitch", ref _transform_pitch);
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Roll", ref _transform_roll);

                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat3("Scale", ref _transform_scale, 0.01f);

                ImGui.SetNextItemWidth(150);
                if(ImGui.ColorPicker4("Color", ref _color))
                {
                    _material!.MaterialParams = 
                    _material!.MaterialParams with { Color0 = _color };
                }
            }


            if (ImGui.CollapsingHeader("Shaders"))
            {
                string Format(ShaderSource vert, ShaderSource frag) => $"{vert.Name??"-"} / {frag.Name??"-"}";

                if (ImGui.BeginCombo("##combo", Format(_vertexShaderSource, _fragmentShaderSource)))
                {
                    void ComboItem(ShaderSource vert, ShaderSource frag)
                    {
                        bool is_selected = _vertexShaderSource==vert && _fragmentShaderSource==frag;
                        if (ImGui.Selectable(Format(vert, frag), is_selected))
                        {
                            _vertexShaderSource = vert;
                            _fragmentShaderSource = frag;
                        }
                        if (is_selected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ComboItem(ColoredTriangle.VertexSource, ColoredTriangle.FragmentSource);
                    ComboItem(InfiniteGrid.VertexSource, InfiniteGrid.FragmentSource);
                    ComboItem(DrawMaterial.VertexSource, DrawMaterial.FragmentSource);

                    ImGui.EndCombo();
                }

                sizeAvail = ImGui.GetContentRegionAvail();



                void HandleCompilationResult(ShaderProgram shaderProgram, ShaderCompilationResult result)
                {
                    (ShaderSource vert, ShaderSource frag)? key = null;

                    if (shaderProgram.ShaderSources.Count == 2)
                        key = (shaderProgram.ShaderSources[0], shaderProgram.ShaderSources[1]);

                    if (result.Success)
                    {
                        foreach (var source in shaderProgram.ShaderSources)
                            _shaderCodeErrors.Remove(source);

                        if (key is not null)
                            _shaderLinkingErrors.Remove(key.Value);

                        return;
                    }

                    foreach (var (shaderSource, error) in result.ShaderErrors!)
                        _shaderCodeErrors[shaderSource] = error;

                    if (key is not null)
                        _shaderLinkingErrors[key.Value] = result.LinkingError!;
                }


                string vertexShaderCode = _vertexShaderSource.Code;

                if (ImGui.InputTextMultiline("Vertex Shader Code", ref vertexShaderCode, 10000,
                    new Vector2(sizeAvail.X, 200)))
                {
                    _vertexShaderSource.UpdateSource(vertexShaderCode, HandleCompilationResult);
                }
                ImGui.TextColored(new Vector4(1, 0, 0, 1), 
                    _shaderCodeErrors.GetValueOrDefault(_vertexShaderSource) 
                    ?? string.Empty);


                string fragmentShaderCode = _fragmentShaderSource.Code;

                if (ImGui.InputTextMultiline("Fragment Shader Code", ref fragmentShaderCode, 10000,
                    new Vector2(sizeAvail.X, 200)))
                {
                    _fragmentShaderSource.UpdateSource(fragmentShaderCode, HandleCompilationResult);
                }
                ImGui.TextColored(new Vector4(1, 0, 0, 1), 
                    _shaderCodeErrors.GetValueOrDefault(_fragmentShaderSource) 
                    ?? string.Empty);


                ImGui.Separator();

                ImGui.TextColored(new Vector4(1, 0, 0, 1),
                    _shaderLinkingErrors.GetValueOrDefault((_vertexShaderSource, _fragmentShaderSource)) 
                    ?? string.Empty);
            }
            ImGui.PopStyleColor(3);
            
            ImGui.End();











            //_transform =
            //    Matrix4x4.CreateScale(_transform_scale) *
            //    Matrix4x4.CreateFromYawPitchRoll(
            //        DEGREES_TO_RADIANS * _transform_yaw,
            //        DEGREES_TO_RADIANS * _transform_pitch,
            //        DEGREES_TO_RADIANS * _transform_roll
            //        ) *
            //    Matrix4x4.CreateTranslation(_transform_pos);

            _sceneFB.Use(_gl);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);




            //for (int i = 0; i < 10000; i++)
            {
                Instances.Render(_gl, _material!, in _viewProjection, CollectionsMarshal.AsSpan(_instanceData));
                Gizmos.Render(_gl, _camera.Rotation, in _viewProjection, CollectionsMarshal.AsSpan(_gizmoPositions));
            }

            ColoredTriangle.Render(_gl, ref _color, in _transform, in _viewProjection);

            Matrix4x4 identity = Matrix4x4.Identity;

            InfiniteGrid.Render(_gl, ref _color, in identity, in _viewProjection);


            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            _gl.Viewport(_window.FramebufferSize);
            _imguiController.Render();
        }
    }
}
