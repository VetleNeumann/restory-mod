namespace RestoryMod.Core;

/// <summary>
/// Two buttons that work as one, such as Cross and R2 working the Cleaning tool: it is pressed when
/// either goes down and released only once both are up. Each method answers whether that button's
/// press or release should reach the game.
/// </summary>
public sealed class EitherButton
{
    private bool firstDown;
    private bool secondDown;

    public bool Press(bool second)
    {
        var otherDown = second ? firstDown : secondDown;
        if (second) secondDown = true;
        else firstDown = true;
        return !otherDown;
    }

    /// <summary>A button whose press was never seen (held since before) still releases.</summary>
    public bool Release(bool second)
    {
        if (second) secondDown = false;
        else firstDown = false;
        return !(second ? firstDown : secondDown);
    }

    public void Reset() => firstDown = secondDown = false;
}
