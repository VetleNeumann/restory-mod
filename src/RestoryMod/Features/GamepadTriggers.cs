using Rewired;
using RestoryMod.Core;

namespace RestoryMod.Features;

/// <summary>
/// L2 and R2 read straight off the gamepad (Rewired's gamepad template), whatever the game binds them
/// to, as buttons: see <see cref="TriggerButton"/>.
/// </summary>
internal static class GamepadTriggers
{
    /// <summary>L2: pulled past halfway this frame, and held down now.</summary>
    public static void Left(out bool pressed, out bool down) => Read(left: true, out pressed, out down, out _);

    /// <summary>R2: pulled past halfway this frame, and held down now.</summary>
    public static void Right(out bool pressed, out bool down) => Read(left: false, out pressed, out down, out _);

    /// <summary>Either trigger off its rest at all, so the game's own trigger input can be ignored.</summary>
    public static bool AnyMoved()
    {
        Read(left: true, out _, out _, out var left);
        Read(left: false, out _, out _, out var right);
        return left || right;
    }

    private static void Read(bool left, out bool pressed, out bool down, out bool moved)
    {
        pressed = down = moved = false;
        if (!ReInput.isReady) return;
        var player = ReInput.players.GetPlayer(GamePlayer.Id);
        if (player is null) return;
        foreach (var joystick in player.controllers.Joysticks)
        {
            var gamepad = joystick.GetTemplate<IGamepadTemplate>();
            if (gamepad is null) continue;
            var trigger = left ? gamepad.leftTrigger : gamepad.rightTrigger;
            pressed |= TriggerButton.Pressed(trigger.value, trigger.valuePrev);
            down |= TriggerButton.Down(trigger.value);
            moved |= trigger.value > 0f;
        }
    }
}
