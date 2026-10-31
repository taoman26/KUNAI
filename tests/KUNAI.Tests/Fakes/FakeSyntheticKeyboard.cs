using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

public sealed class FakeSyntheticKeyboard : ISyntheticKeyboard
{
    public List<string> Sent { get; } = new();

    /// <summary>このショートカットが来たら例外を投げる。</summary>
    public string? ThrowOn { get; set; }

    public Task SendShortcutAsync(string shortcut)
    {
        if (shortcut == ThrowOn)
        {
            throw new InvalidOperationException($"Failed: {shortcut}");
        }

        Sent.Add(shortcut);
        return Task.CompletedTask;
    }
}
