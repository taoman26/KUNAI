using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

/// <summary>実際には待たず、"delay:100" の形でジャーナルに記録する。</summary>
public sealed class FakeDelayProvider : IDelayProvider
{
    private readonly List<string> _journal;

    public FakeDelayProvider(List<string>? journal = null)
    {
        _journal = journal ?? new List<string>();
    }

    public List<TimeSpan> Delays { get; } = new();

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        _journal.Add($"delay:{(int)delay.TotalMilliseconds}");
        return Task.CompletedTask;
    }
}
