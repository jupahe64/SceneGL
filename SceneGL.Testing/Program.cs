using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Silk.NET.Input;
using ImGuiNET;
using SceneGL.Testing;
using System.Diagnostics;
using Silk.NET.Core.Contexts;

//uint buffer = 0;

//var options = WindowOptions.Default;

//options.API = new GraphicsAPI(
//                ContextAPI.OpenGL,
//                ContextProfile.Core,
//                ContextFlags.Debug,
//                new APIVersion(3, 2)
//                );

//var w = Window.Create(options);
//var w2 = Window.Create(options);
//var w3 = Window.Create(options);
//w3.Load += () =>
//{
//    GL gl = w3.CreateOpenGL();

//    w3.SharedContext!.MakeCurrent();

//    gl.CreateBuffers(1, out buffer);

//    gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
//    var error = gl.GetError();
//    gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
//    var error2 = gl.GetError();

//    w2.MakeCurrent();
//    uint lol;
//    gl.CreateBuffers(1, out lol);
//    gl.DeleteBuffer(lol);
//    gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
//    var error3 = gl.GetError();
//    gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
//    var error4 = gl.GetError();


//    Debugger.Break();
//};

//w.Initialize();
//w2.Initialize();
//w3.Initialize();


//return;

new TestWindow().AddToWindowManager();
//new TestWindow().AddToWindowManager();

WindowManager.Run();


//var w = Window.Create(WindowOptions.Default);
//GL? GL = null;


//var w2 = Window.Create(WindowOptions.Default);



//w2.Load += () =>
//{
//    var (hWnd, _, _) = w2.Native!.Win32!.Value;
//    WindowsDarkmodeUtil.SetDarkmodeAware(hWnd);
//};

//new Thread(w2.Run).Start();



//ImGuiController imguiController = null;

//w.Load += async () =>
//{
//    GL = w.CreateOpenGL();

//    imguiController = new ImGuiController(GL, w, w.CreateInput());

//    var (hWnd, _, _) = w.Native!.Win32!.Value;

//    WindowsDarkmodeUtil.SetDarkmodeAware(hWnd);

//    w.Resize += (Silk.NET.Maths.Vector2D<int> size) =>
//    {
//        w.DoUpdate();
//        w.DoRender();
//    };


//};

//w.Update += (deltaTime) =>
//{
//    if (imguiController == null)
//        return;

//    imguiController.Update((float)deltaTime);
//};

//w.Render += (deltaTime) =>
//{
//    if (GL == null)
//        return;

//    GL.ClearColor(0.1f, 0.1f, 0.1f, 0);
//    GL.Clear(ClearBufferMask.ColorBufferBit);

//    ImGui.ShowDemoWindow();


//    imguiController?.Render();
//};

//w.FramebufferResize += (size) =>
//{
//    Console.WriteLine("Framebuffer resized");

//    GL.Viewport(w.Size);
//};

//w.Move += (Silk.NET.Maths.Vector2D<int> obj) =>
//{
//    w.DoUpdate();
//    w.DoRender();
//};



//w.Run();
