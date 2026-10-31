using KUNAI.Core.Domain;
using KUNAI.Core.Services;

namespace KUNAI.Tests.Core;

public class LayerManagerTests
{
    private static (LayerManager Manager, List<int> Changes) Create()
    {
        var manager = new LayerManager();
        var changes = new List<int>();
        manager.LayerChanged += (_, layer) => changes.Add(layer);
        return (manager, changes);
    }

    [Fact]
    public void InitialLayer_IsZero()
    {
        var (manager, _) = Create();

        Assert.Equal(0, manager.ActiveLayer);
    }

    [Fact]
    public void PressB_ActivatesLayer1_AndReleaseReturnsToLayer0()
    {
        var (manager, changes) = Create();

        manager.OnButtonPressed(WiimoteButton.B);
        Assert.Equal(1, manager.ActiveLayer);

        manager.OnButtonReleased(WiimoteButton.B);
        Assert.Equal(0, manager.ActiveLayer);
        Assert.Equal(new[] { 1, 0 }, changes);
    }

    [Fact]
    public void PressHome_ActivatesLayer2_AndReleaseReturnsToLayer0()
    {
        var (manager, changes) = Create();

        manager.OnButtonPressed(WiimoteButton.Home);
        Assert.Equal(2, manager.ActiveLayer);

        manager.OnButtonReleased(WiimoteButton.Home);
        Assert.Equal(0, manager.ActiveLayer);
        Assert.Equal(new[] { 2, 0 }, changes);
    }

    [Theory]
    [InlineData(WiimoteButton.B, WiimoteButton.Home)]
    [InlineData(WiimoteButton.Home, WiimoteButton.B)]
    public void PressBoth_ActivatesLayer3_RegardlessOfOrder(WiimoteButton first, WiimoteButton second)
    {
        var (manager, _) = Create();

        manager.OnButtonPressed(first);
        manager.OnButtonPressed(second);

        Assert.Equal(3, manager.ActiveLayer);
    }

    [Fact]
    public void ReleasingOneOfBoth_FallsBackToTheRemainingLayer()
    {
        var (manager, changes) = Create();
        manager.OnButtonPressed(WiimoteButton.B);
        manager.OnButtonPressed(WiimoteButton.Home);

        manager.OnButtonReleased(WiimoteButton.B);
        Assert.Equal(2, manager.ActiveLayer);

        manager.OnButtonPressed(WiimoteButton.B);
        manager.OnButtonReleased(WiimoteButton.Home);
        Assert.Equal(1, manager.ActiveLayer);

        manager.OnButtonReleased(WiimoteButton.B);
        Assert.Equal(0, manager.ActiveLayer);

        Assert.Equal(new[] { 1, 3, 2, 3, 1, 0 }, changes);
    }

    [Theory]
    [InlineData(WiimoteButton.A)]
    [InlineData(WiimoteButton.One)]
    [InlineData(WiimoteButton.Two)]
    [InlineData(WiimoteButton.Plus)]
    [InlineData(WiimoteButton.Minus)]
    [InlineData(WiimoteButton.Up)]
    [InlineData(WiimoteButton.Down)]
    [InlineData(WiimoteButton.Left)]
    [InlineData(WiimoteButton.Right)]
    public void OtherButtons_DoNotChangeLayer(WiimoteButton button)
    {
        var (manager, changes) = Create();

        manager.OnButtonPressed(button);
        manager.OnButtonReleased(button);

        Assert.Equal(0, manager.ActiveLayer);
        Assert.Empty(changes);
    }

    [Fact]
    public void OtherButtonPressedWhileLayerHeld_KeepsLayer()
    {
        var (manager, _) = Create();
        manager.OnButtonPressed(WiimoteButton.B);

        manager.OnButtonPressed(WiimoteButton.A);

        Assert.Equal(1, manager.ActiveLayer);
    }

    [Fact]
    public void DuplicatePress_DoesNotRaiseEventTwice()
    {
        var (manager, changes) = Create();

        manager.OnButtonPressed(WiimoteButton.B);
        manager.OnButtonPressed(WiimoteButton.B);

        Assert.Single(changes);
    }

    [Fact]
    public void ReleaseWithoutPress_IsIgnored()
    {
        var (manager, changes) = Create();

        manager.OnButtonReleased(WiimoteButton.B);

        Assert.Equal(0, manager.ActiveLayer);
        Assert.Empty(changes);
    }

    [Fact]
    public void Reset_ClearsHeldButtons_AndRaisesEventOnlyIfLayerChanged()
    {
        var (manager, changes) = Create();
        manager.OnButtonPressed(WiimoteButton.B);
        manager.OnButtonPressed(WiimoteButton.Home);

        manager.Reset();
        Assert.Equal(0, manager.ActiveLayer);

        // Reset 後は B を離しただけでは Layer2 に戻らず、押し直しで Layer1 になる。
        manager.OnButtonPressed(WiimoteButton.B);
        Assert.Equal(1, manager.ActiveLayer);

        manager.Reset();
        manager.Reset();
        Assert.Equal(new[] { 1, 3, 0, 1, 0 }, changes);
    }
}
