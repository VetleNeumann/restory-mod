namespace RestoryMod.Core;

/// <summary>What R2 does with the Hint at the workbench: the "R2 hint" setting.</summary>
public enum HintButtonMode
{
    /// <summary>A press shows the hint; it stays lit while R2 is held.</summary>
    Hold,

    /// <summary>A press shows the hint and it stays lit until R2 is pressed again.</summary>
    Toggle,

    /// <summary>R2 leaves the hint alone.</summary>
    Off,
}

/// <summary>
/// R2's hold on the Hint. <see cref="Lit"/> is whether R2 keeps the hint lit now; leaving the
/// workbench ends it in either mode.
/// </summary>
public sealed class HintButton
{
    public bool Lit { get; private set; }

    /// <summary>
    /// Advances one frame from R2's <paramref name="pressed"/> edge and <paramref name="down"/> state.
    /// True when this frame should show the hint.
    /// </summary>
    public bool Update(HintButtonMode mode, bool pressed, bool down, bool atWorkbench)
    {
        if (!atWorkbench || mode == HintButtonMode.Off)
        {
            Lit = false;
            return false;
        }

        if (mode == HintButtonMode.Hold)
        {
            Lit = down;
            return pressed;
        }

        if (!pressed) return false;
        Lit = !Lit;
        return Lit;
    }
}
