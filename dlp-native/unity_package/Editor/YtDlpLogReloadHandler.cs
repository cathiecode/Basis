using UnityEditor;

namespace YtDlp.Editor
{
    [InitializeOnLoad]
    internal static class YtDlpLogReloadHandler
    {
        static YtDlpLogReloadHandler()
        {
            AssemblyReloadEvents.beforeAssemblyReload += YtDlpLogBridge.Shutdown;
        }
    }
}
