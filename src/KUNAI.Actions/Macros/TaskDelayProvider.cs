using KUNAI.Core.Abstractions;

namespace KUNAI.Actions.Macros;

/// <summary><see cref="Task.Delay(TimeSpan, CancellationToken)"/> による既定の待機実装。</summary>
public sealed class TaskDelayProvider : IDelayProvider
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay, cancellationToken);
}
