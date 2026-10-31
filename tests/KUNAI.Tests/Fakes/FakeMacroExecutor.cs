using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

public sealed class FakeMacroExecutor : IMacroExecutor
{
    public List<string[]> Executed { get; } = new();

    public Task ExecuteAsync(IReadOnlyList<string> steps, CancellationToken cancellationToken = default)
    {
        Executed.Add(steps.ToArray());
        return Task.CompletedTask;
    }
}
