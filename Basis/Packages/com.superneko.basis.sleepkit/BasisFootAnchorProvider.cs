using Basis.BasisUI;
using UnityEngine;

public class BasisFootAnchorProvider : BasisMenuActionProvider<BasisMainMenu>
{
    public override bool Hidden => false;

    public override string Title => "Foot anchor";

    public override string IconAddress => "";

    public override int Order => 1000;

    [RuntimeInitializeOnLoadMethod]
    static void BasisFootAnchorProviderInit()
    {
        BasisMainMenu.AddProvider(new BasisFootAnchorProvider());
    }

    public override void RunAction()
    {
        BasisFootAnchor.ToggleAnchor();
    }
}
