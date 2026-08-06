using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace Basis.Scripts.Device_Management
{
    public static class BasisGracefulShutdown
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

            Task.Run(GracefulShutdown);

            return false;
        }

        public static async Task GracefulShutdown()
        {
            Debug.Log("[GracefulShutdown] Shutdown task starting.");

            foreach (var action in Actions)
            {
                try {
                    await action();
                } catch (Exception e)
                {
                    Debug.LogError($"[GracefulShutdown] Shutdown task has failed: {e}");
                }
            }

            Debug.Log("[GracefulShutdown] Shutdown task done.");

            Application.Quit();
        }
    }
}
