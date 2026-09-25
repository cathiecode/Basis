using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Basis.BasisUI;
using UnityEngine;
using System.Collections.Concurrent;
using System.Linq;

namespace com.superneko.basis.desktopcast
{
    using System.Threading;
    using Basis.Scripts.UI.UI_Panels;
    using native;

    static class Utils
    {
        public static void FireAndForget(string title, Func<Task> task)
        {
            Task.Run(task).ContinueWith((t) =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    Debug.Log($"{title} is completed successfully.");
                }
                if (t.IsFaulted)
                {
                    Debug.LogError($"{title} has failed({t.Status}). Exception was {t.Exception}");
                }
            });
        }
    }

    public static class GracefulShutdown
    {
        public static bool HasEvents = false;
        public static bool IsQuitting = true;

        public static ConcurrentStack<Func<Task>> Actions = new();

        public static void Setup()
        {
            if (HasEvents)
            {
                return;
            }

            IsQuitting = false;
            Actions.Clear();

            Application.wantsToQuit -= OnWantsQuit;
            Application.wantsToQuit += OnWantsQuit;
        }

        public static bool OnWantsQuit()
        {
            if (IsQuitting)
            {
                // Second quit request.
                // case 1. User double-clicked exit button
                // case 2. GracefulShutdown() called Application.Quit()
                return true;
            }

            IsQuitting = true;

            Utils.FireAndForget("Graceful shutdown task", PerformGracefulShutdown);

            return false;
        }

        public static async Task PerformGracefulShutdown()
        {
            Debug.Log("[GracefulShutdown] Shutdown task starting.");

            foreach (var action in Actions)
            {
                try
                {
                    await action();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[GracefulShutdown] Shutdown task has failed: {e}");
                }
            }

            Debug.Log("[GracefulShutdown] Shutdown task done.");

            Application.Quit();
        }
    }


    public static class DesktopCastBoot
    {
        const string DEFAULT_GST_PATH = "C:\\Program Files\\gstreamer\\1.0\\msvc_x86_64\\bin";

        [RuntimeInitializeOnLoadMethod]
        public static void Setup()
        {
            var path = Environment.GetEnvironmentVariable("PATH");

            if (path.IndexOf(DEFAULT_GST_PATH) < 0)
            {
                Environment.SetEnvironmentVariable("PATH", $"{DEFAULT_GST_PATH};{path}", EnvironmentVariableTarget.Process);
            }

            BasisMainMenu.AddProvider(new DesktopCastProvider());
            GracefulShutdown.Actions.Push(Teardown);
        }

        public static async Task Teardown()
        {
            await DesktopCast.Instance.Shutdown();
        }
    }

    public interface Window
    {
        public string Title { get; }
    }

    public class Session
    {
        public bool Cancelled;
        public Action CancelEvent = () => { };
        internal Cancellable Cancellable;

        public string Title = string.Empty;
        public string PublishUrl = string.Empty;
        public string WatchUrl = string.Empty;

        public void Cancel()
        {
            if (Cancelled) return;

            Debug.Log($"[DesktopCast] Cancelling stream {PublishUrl}");
            Cancelled = true;
            Cancellable.Cancel();
            CancelEvent.Invoke();
        }
    }

    public class DesktopCast
    {
        public static DesktopCast Instance = new();
        public List<Session> Sessions = new();

        public async Task<Window[]> GetWindowsToCapture()
        {
            return (await Platform.EnumerateWindowToCapture()).Select(w => (Window)w).ToArray();
        }

        public async Task<Session> StartCast(Window window)
        {
            var platformWindow = (Platform.WindowRef)window;

            var key = UnityEngine.Random.Range(0, 999999999).ToString();
            var rtmpUrl = $"rtmp://stream.space.superneko.net/desktopcast/{key}";
            var rtspUrl = $"rtspt://stream.space.superneko.net:554/desktopcast/{key}";

            var cancel = DesktopcastMethods.StartRtmp((ulong)platformWindow.Handle, (uint)platformWindow.processId, rtmpUrl, 30);

            var session = new Session { Title = window.Title, PublishUrl = rtmpUrl, WatchUrl = rtspUrl, Cancellable = cancel };

            Debug.Log($"[DesktopCast] Start on [ {rtmpUrl} ]. Watch on [ {rtspUrl} ].");

            Sessions.Add(session);

            Utils.FireAndForget("Cast session", () => CastSession(session));

            return session;
        }

        public async Task CastSession(Session session)
        {
            // Cast state machine
            if (session.Cancelled) return;

            var url = session.WatchUrl;

            // Spawn player prop
            var propUrl = "https://zipline.space.superneko.net/raw/eGpSou.BEE";
            var propPassword = "97a9bc634009a42f2e876778114ae931eb5dee9ef92d9e6b0f8db6af62bcd865";
            var playerItemKey = new BasisDataStoreItemKeys.ItemKey
            {
                Mode = BundledContentHolder.Mode.Prop,
                PlacementType = BundledContentHolder.PlacementType.SpawnAtRaycast,
                PlacementOverride = BasisPropSpawnPlacement.Unspecified,
                Url = propUrl,
                Pass = propPassword,
                EmbeddedSettings = BasisDataStoreItemKeys.EmbeddedSettings.BEEUrl,
                PinnedSettings = BasisDataStoreItemKeys.PinnedSettings.Default
            };

            Debug.Log("[DesktopCast] Spawning prop");

            await MainThreadDispatcher.RunAsync(() => CachedMetaData.PreloadMetaDataForItem(playerItemKey));
            await MainThreadDispatcher.RunAsync(() => ContentLoader.LoadProp(playerItemKey, BundledContentHolder.NetworkType.Synchronized, false, false, false));

            Debug.Log("[DesktopCast] Waiting for prop spawn");

            Task<DesktopCastDisplay> WaitForNew()
            {
                var tcs = new TaskCompletionSource<DesktopCastDisplay>();

                void OnNew(DesktopCastDisplay display)
                {
                    tcs.SetResult(display);

                    DesktopCastDisplay.OnNewGlobal -= OnNew;
                }

                DesktopCastDisplay.OnNewGlobal += OnNew;

                return tcs.Task;
            }

            // Wait for prop load
            DesktopCastDisplay display;
            while (true)
            {
                var newDisplay = await WaitForNew();

                await MainThreadDispatcher.RunAsync(async () =>
                {
                    if (string.IsNullOrEmpty(newDisplay.SyncedUrl))
                    {
                        Debug.Log("[DesktopCast] Binding session");
                        await newDisplay.Bind(session);
                        display = newDisplay;
                    }
                });

                break;
            }
        }

        public async Task Shutdown()
        {
            Debug.Log("[DesktopCast] Shutdown");
            foreach (var session in Sessions)
            {
                session.Cancel();
            }
        }
    }

    public class DesktopCastProvider : BasisMenuActionProvider<BasisMainMenu>
    {
        public class WindowItemData : MonoBehaviour
        {
            public PanelElementDescriptor Descriptor;
            public PanelButton CastButton;
            public PanelButton StopButton;
        }

        public override string Title => "Desktop cast";

        public override bool Hidden => false;

        public override string IconAddress => "";

        public override int Order => 0;

        BasisMenuPanel _panel;

        public override void RunAction()
        {
            if (BasisMainMenu.ActiveMenuTitle == Title)
            {
                BasisMainMenu.CloseActivePanel();
                return;
            }

            _panel = BasisMainMenu.CreateActiveMenu(BasisMenuPanel.PanelData.Standard("Desktop cast"), BasisMenuPanel.PanelStyles.Page, this);

            _panel.Descriptor.TitleLabel.text = Title;

            RecreateUI();
        }

        public void RecreateUI()
        {
            if (_panel == null) return;

            var parent = _panel.Descriptor.ContentParent;

            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
            }

            Utils.FireAndForget("Asynchronous UI Generation", CreateUI);
        }

        public async Task CreateUI()
        {
            if (_panel == null) return;
            var root = await MainThreadDispatcher.RunSync(() => _panel.Descriptor.ContentParent);

            var parent = await MainThreadDispatcher.RunSync(() => PanelTabPage.CreateVertical(root).Descriptor.ContentParent);

            var windows = await DesktopCast.Instance.GetWindowsToCapture();
            var sessions = DesktopCast.Instance.Sessions;

            await MainThreadDispatcher.RunSync(() =>
            {
                foreach (var session in sessions)
                {
                    if (session.Cancelled) continue;

                    var button = PanelButton.CreateNew(parent);

                    var sessionTitle = session.Title;

                    button.Descriptor.SetTitle($"Casting {sessionTitle}");

                    button.OnClicked += async () =>
                    {
                        session.Cancel();

                        MainThreadDispatcher.Post(RecreateUI);
                    };
                }

                foreach (var window in windows)
                {

                    var button = PanelButton.CreateNew(parent);

                    var windowTitle = string.IsNullOrEmpty(window.Title) ? "<Unnamed>" : window.Title;

                    button.Descriptor.SetTitle($"Cast {windowTitle}");

                    button.OnClicked += async () =>
                    {
                        var session = await DesktopCast.Instance.StartCast(window);

                        MainThreadDispatcher.Post(RecreateUI);
                    };
                }
            });
        }
    }

    public static class MainThreadDispatcher
    {
        private static SynchronizationContext _mainThreadContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void SetMainThreadContext()
        {
            var current = SynchronizationContext.Current;
            _mainThreadContext = current ?? throw new InvalidOperationException();
        }

        public static void Post(Action action)
        {
            if (_mainThreadContext == null)
                throw new InvalidOperationException();

            _mainThreadContext.Post(_ => action(), null);
        }

        public static Task RunSync(Action action)
        {
            var tcs = new TaskCompletionSource<object>();
            Post(async () =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public static Task<TResult> RunSync<TResult>(Func<TResult> func)
        {
            var tcs = new TaskCompletionSource<TResult>();
            Post(async () =>
            {
                try
                {
                    var res = func();
                    tcs.SetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        public static Task RunAsync(Func<Task> func)
        {
            var tcs = new TaskCompletionSource<object>();
            Post(async () =>
            {
                try
                {
                    await func();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }


        public static Task<TResult> RunAsync<TResult>(Func<Task<TResult>> func)
        {
            var tcs = new TaskCompletionSource<TResult>();
            Post(async () =>
            {
                try
                {
                    var res = await func();
                    tcs.SetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }
    }

    public static class Platform
    {
        public struct WindowRef : Window
        {
            public string Title { get; set; }
            public IntPtr Handle;

            public uint dwStyle;
            public uint dwExStyle;
            public ulong processId;

            public override readonly string ToString()
            {
                var title = string.IsNullOrEmpty(Title) ? "<Unnamed or error>" : Title;

                return $"MSWindows window hwnd: {Handle}, title: {title} dwStyle: {dwStyle:X} dwExStyle: {dwExStyle:X}";
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public override string ToString()
                => $"({Left}, {Top}) - ({Right}, {Bottom})";
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWINFO
        {
            public uint cbSize;
            public RECT rcWindow;
            public RECT rcClient;
            public uint dwStyle;
            public uint dwExStyle;
            public uint dwWindowStatus;
            public uint cxWindowBorders;
            public uint cyWindowBorders;
            public ushort atomWindowType;
            public ushort wCreatorVersion;
        }

        [DllImport("user32.dll")]
        static extern int EnumWindows(EnumWindowsDelegate lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowInfo(IntPtr hwnd, ref WINDOWINFO pwi);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern ulong GetWindowThreadProcessId(IntPtr hwnd, ref ulong pid);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        static extern int GetWindowTextW(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpString, int nMaxCount);

        [return: MarshalAs(UnmanagedType.Bool)]
        delegate bool EnumWindowsDelegate(IntPtr hWnd, IntPtr lParam);

        public static async Task<WindowRef[]> EnumerateWindowToCapture()
        {
            List<WindowRef> windowRefs = new();

            EnumWindowsDelegate callback = (hWnd, lParam) =>
            {
                var info = new WINDOWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<WINDOWINFO>()
                };

                if (!GetWindowInfo(hWnd, ref info)) return true;

                const uint WS_VISIBLE = 0x10000000;
                const uint WS_CHILD = 0x40000000;
                const uint WS_POPUP = 0x80000000;
                const uint WS_SYSMENU = 0x00080000;
                const uint WS_EX_TOOLWINDOW = 0x00000080;
                const uint WS_EX_APPWINDOW = 0x00040000;

                // if ((((info.dwStyle & (WS_CHILD | WS_POPUP | WS_SYSMENU)) > 0) || ((info.dwExStyle & WS_EX_TOOLWINDOW) > 0)) && ()) return true;

                var isVisible = IsWindowVisible(hWnd);
                var isChildWindowLike = (info.dwStyle & (WS_CHILD | WS_POPUP)) > 0 || ((info.dwExStyle & WS_EX_TOOLWINDOW) > 0);
                // var isChildWindowLike = false;
                var isForcedInTaskBar = (info.dwExStyle & WS_EX_APPWINDOW) != 0;

                if (isVisible && (!isChildWindowLike || isForcedInTaskBar))
                {
                    ulong pid = 0;
                    var threadId = GetWindowThreadProcessId(hWnd, ref pid);

                    if (threadId != 0)
                    {
                        windowRefs.Add(new WindowRef { Handle = hWnd, Title = GetWindowTitle(hWnd), dwStyle = info.dwStyle, dwExStyle = info.dwExStyle, processId = pid });
                    }
                }

                return true;
            };

            var result = EnumWindows(callback, IntPtr.Zero);

            if (result == 0)
            {
                throw new Exception("Failed to enumerate windows");
            }

            return windowRefs.ToArray();
        }

        static string GetWindowTitle(IntPtr hWnd)
        {
            var titleBulider = new StringBuilder(1024);
            var titleResult = GetWindowTextW(hWnd, titleBulider, titleBulider.Capacity);
            var title = titleResult == 0 ? "" : titleBulider.ToString();

            return title;
        }
    }
}
