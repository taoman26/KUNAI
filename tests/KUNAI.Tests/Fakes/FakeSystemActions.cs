using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Tests.Fakes;

public sealed class FakeSystemActions : ISystemActions
{
    public List<StandardAction> Executed { get; } = new();

    public StandardAction? ThrowOn { get; set; }

    public Task ExecuteAsync(StandardAction action)
    {
        if (action == ThrowOn)
        {
            throw new InvalidOperationException($"Failed: {action}");
        }

        Executed.Add(action);
        return Task.CompletedTask;
    }
}
