using RestoryMod.Core;

namespace RestoryMod.Core.Tests;

public class ButtonSwapTests
{
    private const int Notepad = 90, Inventory = 67, Other = 29;
    private const int X = 2, Y = 3, A = 0;

    private static readonly ButtonSwap Swap = new(Notepad, X, Inventory, Y);

    [Fact]
    public void Each_action_moves_to_the_others_button()
    {
        Assert.Equal(Y, Swap.Moved(Notepad, X));
        Assert.Equal(X, Swap.Moved(Inventory, Y));
    }

    [Theory]
    [InlineData(Other, X)] // another action on the same button stays
    [InlineData(Notepad, A)] // a second binding of the action, on another button, stays
    [InlineData(Inventory, X)] // already on the other button: not swapped twice
    public void Other_bindings_stay(int actionId, int element)
    {
        Assert.Null(Swap.Moved(actionId, element));
    }

    [Fact]
    public void Reversed_moves_them_back()
    {
        Assert.Equal(X, Swap.Reversed.Moved(Notepad, Y));
        Assert.Equal(Y, Swap.Reversed.Moved(Inventory, X));
    }
}
