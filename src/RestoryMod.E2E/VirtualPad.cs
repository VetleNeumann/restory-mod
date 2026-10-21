using System;
using System.Collections.Generic;
using System.Linq;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using RestoryMod.Features;
using Rewired;

namespace RestoryMod.E2E;

/// <summary>
/// A virtual Xbox 360 pad (ViGEmBus) that takes over the game's Rewired player for a run: the real
/// pads come off the player, so a bumped stick can't break the run, and go back on <see cref="Dispose"/>.
/// The game and the mod see it as any other gamepad (see docs/adr/0002-e2e-drives-real-input.md).
/// </summary>
internal sealed class VirtualPad : IDisposable
{
    private readonly ViGEmClient client;
    private readonly IXbox360Controller pad;
    private readonly List<Joystick> realPads = new();
    private Joystick? joystick;

    private VirtualPad(ViGEmClient client, IXbox360Controller pad)
    {
        this.client = client;
        this.pad = pad;
    }

    /// <summary>Plugs the pad in; <see cref="TakeOver"/> once Rewired has seen it.</summary>
    public static VirtualPad Connect(out List<Joystick> before)
    {
        before = ReInput.controllers.Joysticks.ToList();
        var client = new ViGEmClient();
        var pad = client.CreateXbox360Controller();
        pad.AutoSubmitReport = false;
        pad.Connect();
        return new VirtualPad(client, pad);
    }

    /// <summary>
    /// Once a joystick shows up that wasn't there before connecting: gives it the game's player in place
    /// of the real pads. False while Rewired hasn't seen it yet.
    /// </summary>
    public bool TakeOver(List<Joystick> before)
    {
        joystick = ReInput.controllers.Joysticks.FirstOrDefault(j => !before.Contains(j));
        if (joystick is null) return false;
        var player = ReInput.players.GetPlayer(GamePlayer.Id);
        realPads.AddRange(player.controllers.Joysticks.Where(j => j != joystick));
        foreach (var real in realPads) player.controllers.RemoveController(real);
        player.controllers.AddController(joystick, removeFromOtherPlayers: true);
        return true;
    }

    /// <summary>Gives the real pads back, so the player can look around at a --stop-at stop.</summary>
    public void HandBack()
    {
        var player = ReInput.players.GetPlayer(GamePlayer.Id);
        if (joystick is not null) player.controllers.RemoveController(joystick);
        foreach (var real in realPads) player.controllers.AddController(real, removeFromOtherPlayers: true);
        realPads.Clear();
    }

    /// <summary>Start pressed on a real pad this frame: the player asks to abort the run.</summary>
    public bool AbortPressed() => realPads.Any(j => j.GetTemplate<IGamepadTemplate>()?.start.justPressed == true);

    public void Button(PadButton button, bool down)
    {
        pad.SetButtonState(button switch
        {
            PadButton.A => Xbox360Button.A,
            PadButton.B => Xbox360Button.B,
            PadButton.X => Xbox360Button.X,
            PadButton.Y => Xbox360Button.Y,
            PadButton.LB => Xbox360Button.LeftShoulder,
            PadButton.RB => Xbox360Button.RightShoulder,
            PadButton.Start => Xbox360Button.Start,
            PadButton.Back => Xbox360Button.Back,
            PadButton.Up => Xbox360Button.Up,
            PadButton.Down => Xbox360Button.Down,
            PadButton.Left => Xbox360Button.Left,
            PadButton.Right => Xbox360Button.Right,
            _ => throw new ArgumentOutOfRangeException(nameof(button)),
        }, down);
        pad.SubmitReport();
    }

    /// <summary>A trigger pulled from 0 (rest) to 1 (fully).</summary>
    public void Trigger(bool right, float pull)
    {
        pad.SetSliderValue(right ? Xbox360Slider.RightTrigger : Xbox360Slider.LeftTrigger,
            (byte)Math.Round(Clamp(pull, 0f, 1f) * 255f));
        pad.SubmitReport();
    }

    /// <summary>A stick tilted from -1 to 1 on each axis, y up.</summary>
    public void Stick(bool right, Vec2 tilt)
    {
        pad.SetAxisValue(right ? Xbox360Axis.RightThumbX : Xbox360Axis.LeftThumbX, Axis(tilt.X));
        pad.SetAxisValue(right ? Xbox360Axis.RightThumbY : Xbox360Axis.LeftThumbY, Axis(tilt.Y));
        pad.SubmitReport();
    }

    /// <summary>Everything let go.</summary>
    public void Release()
    {
        pad.ResetReport();
        pad.SubmitReport();
    }

    public void Dispose()
    {
        HandBack();
        try
        {
            pad.Disconnect();
        }
        finally
        {
            client.Dispose();
        }
    }

    private static short Axis(float value) => (short)Math.Round(Clamp(value, -1f, 1f) * short.MaxValue);

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
}

/// <summary>The pad's buttons, by their Xbox names (A is Cross on a PlayStation pad).</summary>
internal enum PadButton
{
    A,
    B,
    X,
    Y,
    LB,
    RB,
    Start,
    Back,
    Up,
    Down,
    Left,
    Right,
}
