using System.ComponentModel;
using System.Diagnostics;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;
using KUNAI.Core.Services;

namespace KUNAI.Platform.Linux;

/// <summary>
/// Linux 向けの標準アクション。外部コマンド(xdg-open / pactl / loginctl など)に委譲する。
/// 各アクションは候補を順に試し、実行ファイルが見つかった最初のものを使う。
/// </summary>
public sealed class LinuxSystemActions : ISystemActions
{
    /// <summary>Debian 系の代替システム(x-terminal-emulator)を先頭に、主要なデスクトップ環境の端末を順に試す。</summary>
    private static readonly string[] TerminalCandidates =
    {
        "x-terminal-emulator", "gnome-terminal", "konsole", "xfce4-terminal", "xterm"
    };

    private readonly ISyntheticKeyboard _keyboard;
    private readonly IProcessLauncher _launcher;
    private readonly SystemActionsOptions _options;

    public LinuxSystemActions(ISyntheticKeyboard keyboard, IProcessLauncher launcher, SystemActionsOptions options)
    {
        _keyboard = keyboard ?? throw new ArgumentNullException(nameof(keyboard));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public Task ExecuteAsync(StandardAction action)
    {
        switch (action)
        {
            case StandardAction.LaunchBrowser:
                StartFirstAvailable(new[] { new Command("xdg-open", _options.BrowserUrl) });
                return Task.CompletedTask;

            case StandardAction.LaunchTerminal:
                var terminals = TerminalCandidates.Select(t => new Command(t)).ToList();
                if (_options.TerminalCommand is { Length: > 0 } custom)
                {
                    terminals.Insert(0, new Command(custom));
                }

                StartFirstAvailable(terminals);
                return Task.CompletedTask;

            case StandardAction.LockScreen:
                StartFirstAvailable(new[]
                {
                    new Command("loginctl", "lock-session"),
                    new Command("xdg-screensaver", "lock"),
                    new Command("xflock4")
                });
                return Task.CompletedTask;

            case StandardAction.VolumeUp:
                return AdjustVolumeAsync($"+{_options.VolumeStepPercent}%", "VolumeUp");
            case StandardAction.VolumeDown:
                return AdjustVolumeAsync($"-{_options.VolumeStepPercent}%", "VolumeDown");
            case StandardAction.Mute:
                return RunPactlOrKeyAsync(new Command("pactl", "set-sink-mute", "@DEFAULT_SINK@", "toggle"), "VolumeMute");

            // ブラウザの戻る/進むは、メディアキーより Alt+←/→ の方が Linux のブラウザで確実に効く。
            case StandardAction.BrowserBack:
                return _keyboard.SendShortcutAsync("Alt+Left");
            case StandardAction.BrowserForward:
                return _keyboard.SendShortcutAsync("Alt+Right");

            default:
                throw new NotSupportedException($"Standard action '{action}' is not supported.");
        }
    }

    private Task AdjustVolumeAsync(string delta, string fallbackKey) =>
        RunPactlOrKeyAsync(new Command("pactl", "set-sink-volume", "@DEFAULT_SINK@", delta), fallbackKey);

    /// <summary>pactl が無い環境(PulseAudio/PipeWire 非搭載)ではメディアキーの送出に切り替える。</summary>
    private Task RunPactlOrKeyAsync(Command pactl, string fallbackKey)
    {
        try
        {
            Start(pactl);
            return Task.CompletedTask;
        }
        catch (Win32Exception)
        {
            return _keyboard.SendShortcutAsync(fallbackKey);
        }
    }

    private void StartFirstAvailable(IReadOnlyList<Command> candidates)
    {
        foreach (var command in candidates)
        {
            try
            {
                Start(command);
                return;
            }
            catch (Win32Exception)
            {
                // 実行ファイルが無ければ次の候補へ。
            }
        }

        throw new InvalidOperationException(
            $"None of the required commands could be started. Tried: {string.Join(", ", candidates.Select(c => c.FileName))}.");
    }

    private void Start(Command command)
    {
        var info = new ProcessStartInfo(command.FileName) { UseShellExecute = false };
        foreach (var argument in command.Arguments)
        {
            info.ArgumentList.Add(argument);
        }

        _launcher.Start(info);
    }

    private sealed record Command(string FileName, params string[] Arguments);
}
