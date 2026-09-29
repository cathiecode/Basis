using Basis.EventDriver;
using UnityEngine;

namespace com.superneko.basis.masque.native
{
    public class MasqueInputSystem
    {
        [RuntimeInitializeOnLoadMethod]
        public static void Initialize()
        {
            BasisEventDriver.OnUpdate += OnUpdate;
        }

        public static void OnUpdate()
        {
        }
    }
}
