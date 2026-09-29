using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Basis;
using UnityEngine;

namespace com.superneko.basis.desktopcast
{
    public class DesktopCastDisplay : MonoBehaviour
    {
        public static Action<DesktopCastDisplay> OnNewGlobal = (_) => {};

        Session _session;

        public BasisMediaPlayer MediaPlayer;
        public BasisMediaPlayerNetworking MediaPlayerNetworking;

        public BasisProp Prop;

        public string SyncedUrl => MediaPlayerNetworking.SyncedUrl;

        void Start()
        {
            Debug.Log("[DesktopCast] Prop spawned");
            OnNewGlobal.Invoke(this);
        }

        public async Task Bind(Session session)
        {
            if (!string.IsNullOrEmpty(SyncedUrl) || _session is not null)
            {
                Debug.Log("[DesktopCast] Tried to bind already occupied display. skipping.");
            }

            _session = session;

            _session.CancelEvent += DestroySelfOnCancel;

            await MediaPlayerNetworking.SetUrl(session.WatchUrl);

            // FIXME: Needs appropriate api to do that
            MediaPlayer.Volume = 0;
            MediaPlayer.Mute = true;
            MediaPlayer.AudioComponent.VolumeGain = 0;
            MediaPlayer.AudioComponent.Mute = true;
        }

        void OnDestroy()
        {
            if (_session is not null)
            {
                _session.CancelEvent -= DestroySelfOnCancel;
                _session.Cancel();
            }
        }

        void DestroySelfOnCancel()
        {
            Debug.Log("[DesktopCast] Session is cancelled. Destroying prop.");
            _session.CancelEvent -= DestroySelfOnCancel;

            MainThreadDispatcher.RunSync(() =>
            {
                var (netId, self) = BasisRuntimeSpawnRegistry.SpawnedGameobjects.FirstOrDefault((kvp) => kvp.Value == gameObject);

                if (string.IsNullOrEmpty(netId))
                {
                    Debug.LogError("[DesktopCast] Failed to get netid for this display.");
                    return;
                }

                BasisNetworkSpawnItem.RequestGameObjectUnLoad(netId);
            });
        }
    }
}
