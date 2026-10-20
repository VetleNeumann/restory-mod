namespace RestoryMod.Core;

/// <summary>
/// Two actions trading buttons: <see cref="ActionA"/> moves from <see cref="ElementA"/> to
/// <see cref="ElementB"/> and <see cref="ActionB"/> the other way. Element ids are the controller's own.
/// </summary>
public readonly record struct ButtonSwap(int ActionA, int ElementA, int ActionB, int ElementB)
{
    /// <summary>The element a binding moves to, or null when the swap leaves it alone.</summary>
    public int? Moved(int actionId, int element)
    {
        if (actionId == ActionA && element == ElementA) return ElementB;
        if (actionId == ActionB && element == ElementB) return ElementA;
        return null;
    }

    /// <summary>The swap that undoes this one.</summary>
    public ButtonSwap Reversed => new(ActionA, ElementB, ActionB, ElementA);
}
