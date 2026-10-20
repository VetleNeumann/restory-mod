using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class BumperClaimTests
{
    private const int L1 = -1;
    private const int R1 = 1;

    private static BumperTarget Claim(int step, bool toolWindow = false, bool inventory = false, bool mail = false,
        bool browser = false, bool fade = false) =>
        BumperClaim.For(step, new BumperScreens(toolWindow, inventory, mail, browser, fade));

    [Fact]
    public void L1_fades_the_device_when_nothing_on_screen_claims_it()
    {
        Assert.Equal(BumperTarget.Fade, Claim(L1, fade: true));
    }

    [Fact]
    public void R1_never_fades()
    {
        Assert.Equal(BumperTarget.None, Claim(R1, fade: true));
    }

    [Fact]
    public void Open_screens_claim_L1_before_the_fade()
    {
        Assert.Equal(BumperTarget.ToolWindow, Claim(L1, toolWindow: true, inventory: true, fade: true));
        Assert.Equal(BumperTarget.Inventory, Claim(L1, inventory: true, fade: true));
        Assert.Equal(BumperTarget.MailFolders, Claim(L1, mail: true, browser: true, fade: true));
        Assert.Equal(BumperTarget.Browser, Claim(L1, browser: true, fade: true));
    }

    [Fact]
    public void Nothing_claims_L1_away_from_the_workbench()
    {
        Assert.Equal(BumperTarget.None, Claim(L1));
    }
}
