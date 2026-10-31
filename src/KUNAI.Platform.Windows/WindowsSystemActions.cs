using System.ComponentModel;
using System.Diagnostics;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Platform.Windows.Native;

namespace KUNAI.Platform.Windows;

/// <summary>Windows 向けの標準アクション。音量・ブラウザ操作はメディアキー/ブラウザキーの送出で行う。</summary>
public sealed class WindowsSystemActions : ISystemActions
{
    private static readonly string[] TerminalCandidates = { "wt.exe", "powershell.exe", "cmd.exe" };

    private readonly ISyntheticKeyboard _keyboard;
    private readonly IProcessLauncher _launcher;
    private readonly SystemActionsOptions _options;
    private readonly Func<bool> _lockWorkStation;

    public WindowsSystemActions(ISyntheticKeyboard keyboard, IProcessLauncher launcher, SystemActionsOptions options)
        : this(keyboard, launcher, options, LockWorkStationNative)
    {
    }

    internal WindowsSystemActions(
        ISyntheticKeyboard keyboard,
        IProcessLauncher launcher,
        SystemActionsOptions options,
        Func<bool> lockWorkStation)
    {
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _lockWorkStation = lockWorkStation ?? throw new ArgumentNullException(nameof(lockWorkStation));
    }

    public Task ExecuteAsync(StandardAction action)
    {
        switch (action)
        {
            case StandardAction.LaunchBrowser:
                _launcher.Start(new ProcessStartInfo(_options.BrowserUrl) { UseShellExecute = true });
                return Task.CompletedTask;

            case StandardAction.LaunchTerminal:
                LaunchTerminal();
                return Task.CompletedTask;

            case StandardAction.LockScreen:
                if (!_lockWorkStation())
                {
                    throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                        "LockWorkStation failed.");
                }

                return Task.CompletedTask;

            case StandardAction.VolumeUp:
                return _keyboard.SendShortcutAsync("VolumeUp");
            case StandardAction.VolumeDown:
                return _keyboard.SendShortcutAsync("VolumeDown");
            case StandardAction.Mute:
                return _keyboard.SendShortcutAsync("VolumeMute");
            case StandardAction.BrowserBack:
                return _keyboard.SendShortcutAsync("BrowserBack");
            case StandardAction.BrowserForward:
                return _keyboard.SendShortcutAsync("BrowserForward");

            default:
                throw new NotSupportedException($"Standard action '{action}' is not supported.");
        }
    }

    private void LaunchTerminal()
    {
        var candidates = _options.TerminalCommand is { Length: > 0 } custom
            ? new[] { custom }.Concat(TerminalCandidates)
            : TerminalCandidates;

        foreach (var command in candidates)
        {
            try
            {
                _launcher.Start(new ProcessStartInfo(command) { UseShellExecute = true });
                return;
            }
            catch (Win32Exception)
            {
                // 見つからなければ次の候補へ。
            }
        }

        throw new InvalidOperationException($"No terminal could be started. Tried: {string.Join(", ", candidates)}.");
    }

    private static bool LockWorkStationNative()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsSystemActions is only supported on Windows.");
        }

        return NativeMethods.LockWorkStation();
    }
}
