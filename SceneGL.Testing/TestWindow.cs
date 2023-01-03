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

namespace SceneGL.Testing
{
    internal class TestWindow
    {
        private static object _glLock = new object();

        private readonly IWindow _window;
        private GL? _gl;
        private IInputContext? _input;
        private ImGuiController? _imguiController;

        private ShaderSource _vertexShaderSource = InfiniteGrid.VertexSource;
        private ShaderSource _fragmentShaderSource = InfiniteGrid.FragmentSource;
        private string _shaderCodeError = string.Empty;

        private Vector4 _color = Vector4.Zero;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _viewProjection = Matrix4x4.Identity;
        private Vector3 _transform_pos = Vector3.Zero;
        private float _transform_yaw = 0;
        private float _transform_pitch = 0;
        private float _transform_roll = 0;
        private Vector3 _transform_scale = new Vector3(1f, 0.7f, 1);
        private Vector2 _previous_mousePos = Vector2.Zero;

        private Camera _camera = new Camera(new Vector3(-10, 7, 10), Vector3.Zero);

        private Framebuffer? _sceneFB;
        private bool _initialLayoutSetup = false;
        private DockSpace? _mainDock;
        private bool _isSceneHoveredBeforeDrag;

        private List<Instances.InstanceData> _instanceData;
        private Material<Vector4>? _material;

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

            Random rng = new Random(0);

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


            //_instanceData = new Instances.InstanceData[]
            //{
            //    new(Matrix4x4.Identity),
            //    new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(2, 0, 0)),
            //    new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(-2, 0, 0)),

            //    new(Matrix4x4.CreateTranslation(5, 0, 0)),
            //    new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(3, 0, 0)),
            //    new(Matrix4x4.CreateScale(0.5f) * Matrix4x4.CreateTranslation(7, 0, 0)),
            //};
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

                    if (severity != GLEnum.DebugSeverityNotification)
                        Debugger.Break();
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

            _material = Instances.CreateMaterial(_color);

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
            var pMousePos = _previous_mousePos;

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

                _initialLayoutSetup = true;
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

            float aspectRatio = 1;

            bool isSceneHovered = false;

            bool isRightDragging = ImGui.IsMouseDragging(ImGuiMouseButton.Right);

            if (sizeAvail.X > 0 && sizeAvail.Y > 0)
            {
                _sceneFB.SetSize((uint)sizeAvail.X, (uint)sizeAvail.Y);
                _sceneFB.Create(_gl);

                ImGui.Image(new IntPtr(_sceneFB.GetColorTexture(0)), new(sizeAvail.X, sizeAvail.Y),
                    new Vector2(0, 1), new Vector2(1, 0));

                if(ImGui.IsItemHovered())
                {
                    isSceneHovered = true;
                }

                aspectRatio = sizeAvail.X / sizeAvail.Y;
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

            ImGui.PushStyleColor(ImGuiCol.Header, 0x0);
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, 0x33_33_33_33);
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, 0x0);
            if (ImGui.CollapsingHeader("Properties", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat3("Position", ref _transform_pos, 0.01f);

                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Yaw", ref _transform_yaw);
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Pitch", ref _transform_pitch);
                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat("Roll", ref _transform_roll);

                ImGui.SetNextItemWidth(150);
                ImGui.DragFloat3("Scale", ref _transform_scale, 0.01f);

                ImGui.SetNextItemWidth(150);
                if(ImGui.ColorPicker4("Color", ref _color))
                {
                    _material!.SetData(_color);
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
                    ComboItem(Instances.VertexSource, Instances.FragmentSource);

                    ImGui.EndCombo();
                }

                sizeAvail = ImGui.GetContentRegionAvail();

                string vertexShaderCode = _vertexShaderSource.Code;

                if (ImGui.InputTextMultiline("Vertex Shader Code", ref vertexShaderCode, 10000,
                    new Vector2(sizeAvail.X, 200)))
                {
                    _vertexShaderSource.UpdateSource(vertexShaderCode,
                        (_, errorString) =>
                        {
                            _shaderCodeError = errorString ?? string.Empty;
                        });
                }

                string fragmentShaderCode = _fragmentShaderSource.Code;

                if (ImGui.InputTextMultiline("Fragment Shader Code", ref fragmentShaderCode, 10000,
                    new Vector2(sizeAvail.X, 200)))
                {
                    _fragmentShaderSource.UpdateSource(fragmentShaderCode,
                        (_, errorString) =>
                        {
                            _shaderCodeError = errorString ?? string.Empty;
                        });
                }
                ImGui.TextColored(new Vector4(1, 0, 0, 1), _shaderCodeError);
            }
            ImGui.PopStyleColor(3);
            
            ImGui.End();










            const float DEGREES_TO_RADIANS = MathF.PI / 180f;

            _transform =
                Matrix4x4.CreateScale(_transform_scale) *
                Matrix4x4.CreateFromYawPitchRoll(
                    DEGREES_TO_RADIANS * _transform_yaw,
                    DEGREES_TO_RADIANS * _transform_pitch,
                    DEGREES_TO_RADIANS * _transform_roll
                    ) *
                Matrix4x4.CreateTranslation(_transform_pos);

            

            if (isSceneHovered || _isSceneHoveredBeforeDrag)
            {
                if (isRightDragging)
                {
                    Vector2 delta = mousePos - _previous_mousePos;

                    Vector3 right = Vector3.Transform(Vector3.UnitX, _camera.Rotation);
                    _camera.Rotation = Quaternion.CreateFromAxisAngle(right, -delta.Y * 0.002f) * _camera.Rotation;

                    _camera.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -delta.X * 0.002f) * _camera.Rotation;
                }

                float camMoveSpeed = (float)(0.4 * deltaSeconds * 60);

                var keyboard = _input.Keyboards[0];

                if (keyboard.IsKeyPressed(Key.W))
                    _camera.Eye -= Vector3.Transform(Vector3.UnitZ * camMoveSpeed, _camera.Rotation);
                if (keyboard.IsKeyPressed(Key.S))
                    _camera.Eye += Vector3.Transform(Vector3.UnitZ * camMoveSpeed, _camera.Rotation);

                if (keyboard.IsKeyPressed(Key.A))
                    _camera.Eye -= Vector3.Transform(Vector3.UnitX * camMoveSpeed, _camera.Rotation);
                if (keyboard.IsKeyPressed(Key.D))
                    _camera.Eye += Vector3.Transform(Vector3.UnitX * camMoveSpeed, _camera.Rotation);

                if (keyboard.IsKeyPressed(Key.Q))
                    _camera.Eye -= Vector3.UnitY * camMoveSpeed;
                if (keyboard.IsKeyPressed(Key.E))
                    _camera.Eye += Vector3.UnitY * camMoveSpeed;
            }


            //_cam_rotation_smooth = Quaternion.Slerp(_cam_rotation_smooth, _cam_rotation, 0.2f);
            //_cam_eye_smooth = Vector3.Lerp(_cam_eye_smooth, _cam_eye, 0.2f);

            _camera.Animate(deltaSeconds, out var eyeAnimated, out var rotAnimated);

            _viewProjection = 
                Matrix4x4.CreateTranslation(-eyeAnimated) *
                Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(rotAnimated)) *
                NumericsUtil.CreatePerspectiveReversedDepth(1f, aspectRatio, 0.1f);

            _sceneFB.Use(_gl);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);




            //for (int i = 0; i < 10000; i++)
            {
                Instances.Render(_gl, _material!, in _viewProjection, CollectionsMarshal.AsSpan(_instanceData));
            }

            //ColoredTriangle.Render(_gl, ref _color, in _transform, in _viewProjection);

            Matrix4x4 identity = Matrix4x4.Identity;

            InfiniteGrid.Render(_gl, ref _color, in identity, in _viewProjection);


            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            _gl.Viewport(_window.FramebufferSize);
            _imguiController.Render();

            _previous_mousePos = mousePos;
        }
    }
}
