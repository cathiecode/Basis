using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Common;
using Basis.Scripts.Drivers;
using Org.BouncyCastle.Bcpg;
using UnityEditor;
using UnityEngine;

public class BasisFootAnchor
{
    private static BasisLocks.LockContext lookRotationLock = BasisLocks.GetContext(BasisLocks.LookRotation);
    private static BasisLocks.LockContext movementLock = BasisLocks.GetContext(BasisLocks.Movement);

    private const string CONTEXT = "com.superneko.basis.sleepkit.footanchor";

    private static bool Anchored => lookRotationLock.Contains(CONTEXT) || movementLock.Contains(CONTEXT);

    public static void ToggleAnchor()
    {
        if (Anchored)
        {
            UndoAnchor();
        }
        else
        {
            DoAnchor();
        }
    }

    public static void DoAnchor()
    {
        if (Anchored) return;

        lookRotationLock.Add(CONTEXT);
        movementLock.Add(CONTEXT);
    }

    public static void UndoAnchor()
    {
        if (!Anchored) return;

        lookRotationLock.Remove(CONTEXT);
        movementLock.Remove(CONTEXT);
    }
}
