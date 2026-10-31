using KUNAI.Core.Abstractions;
using KUNAI.Core.Shortcuts;
using KUNAI.Platform.Linux.Native;

namespace KUNAI.Platform.Linux;

/// <summary>
/// uinput の仮想キーボードによるキー送出(Linux 専用。X11 / Wayland のどちらでもカーネル層で動作する)。
/// 仮想デバイスは最初の送出時に作成し、破棄時に削除する。
/// </summary>
public sealed class LinuxSyntheticKeyboard : ISyntheticKeyboard, IDisposable
{
    private const string DeviceName = "KUNAI Virtual Keyboard";

    /// <summary>作成直後の仮想デバイスを udev / X サーバーが認識するまでの待ち時間。</summary>
    private static readonly TimeSpan DeviceSettleTime = TimeSpan.FromMilliseconds(300);

    /// <summary>修飾キーや主キーの押下を受け取る側が取りこぼさないための、イベント間隔。</summary>
    private static readonly TimeSpan DefaultEventInterval = TimeSpan.FromMilliseconds(10);

    private readonly Func<IUInputDevice> _deviceFactory;
    private readonly TimeSpan _eventInterval;
    private readonly TimeSpan _settleTime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IUInputDevice? _device;
    private bool _disposed;

    public LinuxSyntheticKeyboard()
        : this(CreateUInputDevice, DefaultEventInterval, DeviceSettleTime)
    {
    }

    internal LinuxSyntheticKeyboard(Func<IUInputDevice> deviceFactory, TimeSpan eventInterval, TimeSpan settleTime)
    {
        _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(deviceFactory));
        _eventInterval = eventInterval;
        _settleTime = settleTime;
    }

    /// <summary>
    /// 例: "Ctrl+C", "Ctrl+Shift+T", "Alt+Tab", "Win+D"。不正な文字列は <see cref="FormatException"/>。
    /// /dev/uinput へのアクセス権が無い場合は <see cref="IOException"/>(設定手順がメッセージに含まれる)。
    /// </summary>
    public async Task SendShortcutAsync(string shortcut)
    {
        var parsed = ShortcutParser.Parse(shortcut);
        var events = LinuxKeyEventBuilder.Build(parsed);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var device = await EnsureDeviceAsync().ConfigureAwait(false);

            try
            {
                for (var i = 0; i < events.Count; i++)
                {
                    device.Emit(events[i]);
                    if (i < events.Count - 1 && _eventInterval > TimeSpan.Zero)
                    {
                        await Task.Delay(_eventInterval).ConfigureAwait(false);
                    }
                }
            }
            catch
            {
                // 押下だけ書き込めて解放が書き込めなかった場合に、修飾キーの固着を防ぐ。
                ReleaseModifiersQuietly(device, parsed.Modifiers);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            _disposed = true;
            _device?.Dispose();
            _device = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IUInputDevice> EnsureDeviceAsync()
    {
        if (_device is not null)
        {
            return _device;
        }

        var device = _deviceFactory();
        if (_settleTime > TimeSpan.Zero)
        {
            await Task.Delay(_settleTime).ConfigureAwait(false);
        }

        _device = device;
        return device;
    }

    private static void ReleaseModifiersQuietly(IUInputDevice device, KeyModifiers modifiers)
    {
        try
        {
            foreach (var release in LinuxKeyEventBuilder.BuildModifierRelease(modifiers))
            {
                device.Emit(release);
            }
        }
        catch
        {
            // 後始末の失敗は元の例外を優先する。
        }
    }

    private static IUInputDevice CreateUInputDevice()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("LinuxSyntheticKeyboard is only supported on Linux.");
        }

        return UInputDevice.Create(DeviceName, LinuxKeyCodes.AllCodes);
    }
}
