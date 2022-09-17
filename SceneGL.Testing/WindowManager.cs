using Silk.NET.Core.Contexts;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.Testing
{
    internal static class WindowManager
    {
        public static IGLContext? SharedContext { get; private set; } = null;

        private static bool s_isRunning = false;

        private static readonly List<IWindow> s_windows = new();

        private static readonly List<IWindow> s_pendingInits = new();

        public static void Add(IWindow window)
        {
            if (s_windows.Contains(window))
                return;

            s_pendingInits.Add(window);
        }

        public static void Run()
        {
            if (s_isRunning)
                return;

            s_isRunning = true;

            while(s_windows.Count > 0 || s_pendingInits.Count > 0)
            {
                if (s_pendingInits.Count > 0)
                {
                    foreach (var window in s_pendingInits)
                    {
                        window.Initialize();
                        s_windows.Add(window);

                        if (SharedContext == null)
                            SharedContext = window.GLContext;
                    }

                    s_pendingInits.Clear();
                }


                for (int i = 0; i < s_windows.Count; i++)
                {
                    var window = s_windows[i];

                    window.DoEvents();
                    if (!window.IsClosing)
                    {
                        window.DoUpdate();
                    }

                    if (!window.IsClosing)
                    {
                        window.DoRender();
                    }

                    if(window.IsClosing)
                    {
                        s_windows.RemoveAt(i);

                        if (window.GLContext == SharedContext && s_windows.Count > 0)
                        {
                            SharedContext = s_windows[0].GLContext;
                        }

                        window.DoEvents();
                        window.Reset();
                        
                        i--;
                    }
                }

                
            }
        }
    }

    internal static class WindowExtensions
    {
        public static void AddToWindowManager(this IWindow window) => WindowManager.Add(window);
    }
}
