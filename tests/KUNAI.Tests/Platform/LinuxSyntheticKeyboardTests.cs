using KUNAI.Core.Shortcuts;
using KUNAI.Platform.Linux;
using KUNAI.Platform.Linux.Native;

namespace KUNAI.Tests.Platform;

/// <summary>Linux 実装のうち OS 非依存の部分(変換表・イベント列・構造体)を検証する。/dev/uinput への書き込み自体は実機・権限付きでのみ確認できる。</summary>
public class LinuxSyntheticKeyboardTests
{
    private sealed class RecordingDevice : IUInputDevice
    {
        public List<LinuxKeyEvent> Events { get; } = new();

        /// <summary>この回数目の Emit で例外を投げる(1 始まり)。0 なら投げない。</summary>
        public int ThrowOnEmitNumber { get; set; }

        public bool Disposed { get; private set; }

        public void Emit(LinuxKeyEvent keyEvent)
        {
            if (ThrowOnEmitNumber != 0 && Events.Count + 1 == ThrowOnEmitNumber)
            {
                Events.Add(default); // 失敗した試行も数える
                throw new IOException("write failed");
            }

            Events.Add(keyEvent);
        }

        public void Dispose() => Disposed = true;
    }

    private static string Describe(IEnumerable<LinuxKeyEvent> events) =>
        string.Join(' ', events.Select(e => $"{(e.IsDown ? "+" : "-")}{e.Code}"));

    private static (LinuxSyntheticKeyboard Keyboard, RecordingDevice Device, Func<int> Created) Create()
    {
        var device = new RecordingDevice();
        var created = 0;
        var keyboard = new LinuxSyntheticKeyboard(
            () =>
            {
                created++;
                return device;
            },
            eventInterval: TimeSpan.Zero,
            settleTime: TimeSpan.Zero);
        return (keyboard, device, () => created);
    }

    [Fact]
    public async Task CtrlC_SendsCtrlDown_C_Down_Up_CtrlUp()
    {
        var (keyboard, device, _) = Create();

        await keyboard.SendShortcutAsync("Ctrl+C");

        Assert.Equal("+29 +46 -46 -29", Describe(device.Events));
    }

    [Fact]
    public async Task CtrlShiftT_PressesModifiersInOrder_AndReleasesInReverse()
    {
        var (keyboard, device, _) = Create();

        await keyboard.SendShortcutAsync("Ctrl+Shift+T");

        Assert.Equal("+29 +42 +20 -20 -42 -29", Describe(device.Events));
    }

    [Fact]
    public async Task AltTab_And_WinD_UseLeftAltAndLeftMeta()
    {
        var (keyboard, device, _) = Create();

        await keyboard.SendShortcutAsync("Alt+Tab");
        await keyboard.SendShortcutAsync("Win+D");

        Assert.Equal("+56 +15 -15 -56 +125 +32 -32 -125", Describe(device.Events));
    }

    [Fact]
    public async Task SingleKeys_AreSentWithoutModifiers()
    {
        var (keyboard, device, _) = Create();

        await keyboard.SendShortcutAsync("Enter");
        await keyboard.SendShortcutAsync("BrowserBack");

        Assert.Equal("+28 -28 +158 -158", Describe(device.Events));
    }

    [Fact]
    public async Task Device_IsCreatedOnceAndReused()
    {
        var (keyboard, _, created) = Create();

        await keyboard.SendShortcutAsync("A");
        await keyboard.SendShortcutAsync("B");

        Assert.Equal(1, created());
    }

    [Fact]
    public async Task Device_IsNotCreated_ForInvalidShortcut()
    {
        var (keyboard, device, created) = Create();

        await Assert.ThrowsAsync<FormatException>(() => keyboard.SendShortcutAsync("Foo+C"));

        Assert.Equal(0, created());
        Assert.Empty(device.Events);
    }

    [Fact]
    public async Task WriteFailure_ReleasesModifiers_AndRethrowsOriginalException()
    {
        var (keyboard, device, _) = Create();
        device.ThrowOnEmitNumber = 4; // Ctrl↓ Shift↓ C↓ まで成功、C↑ で失敗

        await Assert.ThrowsAsync<IOException>(() => keyboard.SendShortcutAsync("Ctrl+Shift+C"));

        // 失敗した試行(default)の後に、Shift↑ Ctrl↑ が送られる。
        Assert.Equal("+29 +42 +46 -0 -42 -29", Describe(device.Events));
    }

    [Fact]
    public async Task DeviceCreationFailure_Propagates_AndNextCallRetries()
    {
        var attempts = 0;
        var device = new RecordingDevice();
        var keyboard = new LinuxSyntheticKeyboard(
            () => ++attempts == 1 ? throw new IOException("no permission") : device,
            TimeSpan.Zero,
            TimeSpan.Zero);

        await Assert.ThrowsAsync<IOException>(() => keyboard.SendShortcutAsync("A"));
        await keyboard.SendShortcutAsync("A");

        Assert.Equal(2, attempts);
        Assert.Equal("+30 -30", Describe(device.Events));
    }

    [Fact]
    public async Task Dispose_DestroysDevice_AndRejectsFurtherUse()
    {
        var (keyboard, device, _) = Create();
        await keyboard.SendShortcutAsync("A");

        keyboard.Dispose();

        Assert.True(device.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => keyboard.SendShortcutAsync("A"));
    }

    [Fact]
    public void EveryKeyCode_HasAnEvdevMapping()
    {
        foreach (var key in Enum.GetValues<KeyCode>())
        {
            Assert.True(LinuxKeyCodes.TryGet(key, out var code), $"No evdev mapping for {key}");
            Assert.NotEqual((ushort)0, code);
        }
    }

    [Fact]
    public void EvdevCodes_AreUnique_ExceptPlusAndEquals()
    {
        var duplicates = Enum.GetValues<KeyCode>()
            .Where(k => k != KeyCode.Plus)
            .Select(k =>
            {
                LinuxKeyCodes.TryGet(k, out var code);
                return code;
            })
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void AllCodes_IncludesModifiers_ForDeviceCapabilityDeclaration()
    {
        var all = LinuxKeyCodes.AllCodes.ToHashSet();

        Assert.Contains(LinuxKeyCodes.KeyLeftCtrl, all);
        Assert.Contains(LinuxKeyCodes.KeyLeftShift, all);
        Assert.Contains(LinuxKeyCodes.KeyLeftAlt, all);
        Assert.Contains(LinuxKeyCodes.KeyLeftMeta, all);
    }

    [Fact]
    public void UserDevStruct_Is1116Bytes_WithNameAndUsbBus()
    {
        var bytes = UInputDevice.BuildUserDev("KUNAI Virtual Keyboard");

        Assert.Equal(1116, bytes.Length);
        Assert.Equal("KUNAI Virtual Keyboard", System.Text.Encoding.ASCII.GetString(bytes, 0, 22));
        Assert.Equal(0, bytes[22]);
        Assert.Equal(0x03, BitConverter.ToUInt16(bytes, 80));
    }

    [Fact]
    public void InputEventStruct_MatchesKernelLayout()
    {
        var bytes = UInputDevice.BuildInputEvent(type: 1, code: 46, value: 1);

        Assert.Equal(2 * IntPtr.Size + 8, bytes.Length); // x64 では 24 バイト
        var offset = 2 * IntPtr.Size;
        Assert.Equal(1, BitConverter.ToUInt16(bytes, offset));
        Assert.Equal(46, BitConverter.ToUInt16(bytes, offset + 2));
        Assert.Equal(1, BitConverter.ToInt32(bytes, offset + 4));
    }

    [Fact]
    public void Constructor_RejectsNullFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LinuxSyntheticKeyboard(null!, TimeSpan.Zero, TimeSpan.Zero));
    }
}
