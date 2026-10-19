using Rewired;
using RestoryMod.Core;

namespace RestoryMod.Features;

/// <summary>
/// L2 and R2 read straight off the gamepad (Rewired's gamepad template), whatever the game binds them
/// to, as buttons: see <see cref="TriggerButton"/>.
/// </summary>
internal static class GamepadTriggers
{
    /// <summary>The game reads gamepad input as this Rewired player.</summary>
    private const int GamePlayerId = 0;

    public static bool LeftPressed() => Read(left: true, out var pressed, out _) && pressed;

    /// <summary>R2: pulled past halfway this frame, and held down now.</summary>
    public static void Right(out bool pressed, out bool down) => Read(left: false, out pressed, out down);

    /// <summary>Either trigger held at all, so the game's own trigger input can be ignored.</summary>
    public static bool AnyHeld()
    {
        Read(left: true, out _, out var leftDown);
        Read(left: false, out _, out var rightDown);
        return leftDown || rightDown;
    }

    private static bool Read(bool left, out bool pressed, out bool down)
    {
        pressed = down = false;
        if (!ReInput.isReady) return false;
        var player = ReInput.players.GetPlayer(GamePlayerId);
        if (player is null) return false;
        foreach (var joystick in player.controllers.Joysticks)
        {
            var gamepad = joystick.GetTemplate<IGamepadTemplate>();
            if (gamepad is null) continue;
            var trigger = left ? gamepad.leftTrigger : gamepad.rightTrigger;
            pressed |= TriggerButton.Pressed(trigger.value, trigger.valuePrev);
            down |= trigger.value > 0f && TriggerButton.Down(trigger.value);
        }
        return true;
    }
}
