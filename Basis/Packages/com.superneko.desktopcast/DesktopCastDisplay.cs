using System;
using System.Collections.Generic;
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
            Debug.Log("[DesktopCast] Prop is destroyed. Cancelling session.");
            _session.CancelEvent -= DestroySelfOnCancel;
            BasisNetworkSpawnItem.RequestGameObjectUnLoad(Prop.ContentInformation.LoadedNetID);
        }
    }
}
