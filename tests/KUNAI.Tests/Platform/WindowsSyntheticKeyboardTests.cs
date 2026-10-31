using KUNAI.Core.Shortcuts;
using KUNAI.Platform.Windows;
using KUNAI.Platform.Windows.Native;

namespace KUNAI.Tests.Platform;

/// <summary>Windows 実装のうち OS 非依存の部分(変換表・イベント列)を検証する。SendInput 自体は Windows 実機でのみ確認できる。</summary>
public class WindowsSyntheticKeyboardTests
{
    private sealed class RecordingInjector : IKeyEventInjector
    {
        public List<IReadOnlyList<KeyEvent>> Calls { get; } = new();

        public Exception? ThrowOnFirstCall { get; set; }

        public void Inject(IReadOnlyList<KeyEvent> events)
        {
            Calls.Add(events);
            if (ThrowOnFirstCall is { } ex && Calls.Count == 1)
            {
                throw ex;
            }
        }
    }

    private static string Describe(IReadOnlyList<KeyEvent> events) =>
        string.Join(' ', events.Select(e => $"{(e.IsKeyUp ? "-" : "+")}{e.VirtualKey:X2}{(e.IsExtended ? "e" : "")}"));

    private static (WindowsSyntheticKeyboard Keyboard, RecordingInjector Injector) Create()
    {
        var injector = new RecordingInjector();
        return (new WindowsSyntheticKeyboard(injector), injector);
    }

    [Fact]
    public async Task CtrlC_SendsCtrlDown_C_Down_Up_CtrlUp()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Ctrl+C");

        Assert.Equal("+A2 +43 -43 -A2", Describe(Assert.Single(injector.Calls)));
    }

    [Fact]
    public async Task CtrlShiftT_PressesModifiersInOrder_AndReleasesInReverse()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Ctrl+Shift+T");

        Assert.Equal("+A2 +A0 +54 -54 -A0 -A2", Describe(Assert.Single(injector.Calls)));
    }

    [Fact]
    public async Task AltTab_UsesLeftMenuKey()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Alt+Tab");

        Assert.Equal("+A4 +09 -09 -A4", Describe(Assert.Single(injector.Calls)));
    }

    [Fact]
    public async Task WinD_MarksWindowsKeyAsExtended()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Win+D");

        Assert.Equal("+5Be +44 -44 -5Be", Describe(Assert.Single(injector.Calls)));
    }

    [Fact]
    public async Task SingleKeys_AreSentWithoutModifiers()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Enter");
        await keyboard.SendShortcutAsync("BrowserBack");
        await keyboard.SendShortcutAsync("Left");

        Assert.Equal(
            new[] { "+0D -0D", "+A6 -A6", "+25e -25e" },
            injector.Calls.Select(Describe));
    }

    [Fact]
    public async Task AllFourModifiers_AreSupported()
    {
        var (keyboard, injector) = Create();

        await keyboard.SendShortcutAsync("Ctrl+Shift+Alt+Win+F5");

        Assert.Equal("+A2 +A0 +A4 +5Be +74 -74 -5Be -A4 -A0 -A2", Describe(Assert.Single(injector.Calls)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Foo+C")]
    [InlineData("Ctrl+Nonsense")]
    public async Task InvalidShortcut_ThrowsFormatException_AndInjectsNothing(string shortcut)
    {
        var (keyboard, injector) = Create();

        await Assert.ThrowsAsync<FormatException>(() => keyboard.SendShortcutAsync(shortcut));

        Assert.Empty(injector.Calls);
    }

    [Fact]
    public async Task InjectionFailure_ReleasesModifiers_AndRethrowsOriginalException()
    {
        var (keyboard, injector) = Create();
        injector.ThrowOnFirstCall = new InvalidOperationException("boom");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => keyboard.SendShortcutAsync("Ctrl+Shift+C"));

        Assert.Equal("boom", ex.Message);
        Assert.Equal(2, injector.Calls.Count);
        Assert.Equal("-A0 -A2", Describe(injector.Calls[1]));
    }

    [Fact]
    public void EveryKeyCode_HasAVirtualKeyMapping()
    {
        foreach (var key in Enum.GetValues<KeyCode>())
        {
            Assert.True(VirtualKeyMap.TryGet(key, out var vk, out _), $"No VK mapping for {key}");
            Assert.NotEqual((ushort)0, vk);
        }
    }

    [Fact]
    public void VirtualKeys_AreUnique_ExceptPlusAndEquals()
    {
        var duplicates = Enum.GetValues<KeyCode>()
            .Where(k => k != KeyCode.Plus)
            .Select(k =>
            {
                VirtualKeyMap.TryGet(k, out var vk, out _);
                return vk;
            })
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Constructor_RejectsNullInjector()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowsSyntheticKeyboard(null!));
    }

    [Fact]
    public void PublicConstructor_ThrowsOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Throws<PlatformNotSupportedException>(() => new WindowsSyntheticKeyboard());
    }

    [Fact]
    public void NativeInputStruct_HasExpectedSize()
    {
        var expected = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(expected, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
