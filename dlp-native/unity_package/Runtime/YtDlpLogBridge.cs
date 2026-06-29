using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using YtDlp.Native;

namespace YtDlp
{
    /// <summary>Routes native log messages onto Unity's main thread.</summary>
    internal static class YtDlpLogBridge
    {
        // The native library retains this function pointer, so the delegate
        // must remain rooted until it is explicitly unregistered.
        private static readonly NativeLib.LogCallback Callback = OnNativeLog;
        private static readonly object Gate = new object();

        private static SynchronizationContext _unityContext;
        private static bool _registered;

        internal static void Initialize()
        {
            lock (Gate)
            {
                if (_registered)
                    return;

                _unityContext = SynchronizationContext.Current;
                if (_unityContext == null)
                {
                    // Do not make native functionality depend on diagnostic
                    // logging. The normal DlpBootstrap path initializes this
                    // bridge from Unity's main thread.
                    return;
                }

                NativeLib.unity_dlp_set_log_callback(Callback);
                Application.quitting += Shutdown;
                _registered = true;
            }
        }

        internal static void Shutdown()
        {
            lock (Gate)
            {
                if (!_registered)
                    return;

                // Native clear waits for callbacks currently in flight.
                NativeLib.unity_dlp_clear_log_callback();
                Application.quitting -= Shutdown;
                _registered = false;
                _unityContext = null;
            }
        }

#if ENABLE_IL2CPP
        [AOT.MonoPInvokeCallback(typeof(NativeLib.LogCallback))]
#endif
        private static void OnNativeLog(int level, IntPtr message, int length)
        {
            try
            {
                if (message == IntPtr.Zero || length < 0)
                    return;

                var bytes = new byte[length];
                if (length > 0)
                    Marshal.Copy(message, bytes, 0, length);
                var text = Encoding.UTF8.GetString(bytes);
                var context = _unityContext;
                if (context != null)
                    context.Post(_ => WriteToUnity(level, text), null);
            }
            catch (Exception e)
            {
                // Never allow a managed exception to cross the native ABI.
                Console.Error.WriteLine($"[YtDlp] Failed to receive native log: {e}");
            }
        }

        private static void WriteToUnity(int level, string message)
        {
            var formatted = $"[YtDlp/native] {message}";
            switch (level)
            {
                case 1:
                    Debug.LogError(formatted);
                    break;
                case 2:
                    Debug.LogWarning(formatted);
                    break;
                default:
                    Debug.Log(formatted);
                    break;
            }
        }
    }
}
