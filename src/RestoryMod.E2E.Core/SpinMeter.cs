namespace RestoryMod.E2E;

/// <summary>
/// How fast the device spins under a held stick: the mean turn per frame, over the frames it turned. The
/// game turns it by a fixed amount each frame, not scaled by frame time, so frames are the unit; the
/// frames before the stick takes hold and after it lets go don't count.
/// </summary>
public sealed class SpinMeter
{
    private float total;
    private int frames;

    /// <summary>Records one frame's turn, in degrees.</summary>
    public void Frame(float degrees)
    {
        if (degrees <= 0f) return;
        total += degrees;
        frames++;
    }

    public float DegreesPerFrame => frames == 0 ? 0f : total / frames;
}
