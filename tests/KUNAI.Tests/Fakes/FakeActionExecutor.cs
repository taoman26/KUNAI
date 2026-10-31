using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

/// <summary>実行されたアクションを共有ジャーナルに記録する。</summary>
public sealed class FakeActionExecutor : IActionExecutor
{
    private readonly List<string> _journal;

    public FakeActionExecutor(List<string>? journal = null)
    {
        _journal = journal ?? new List<string>();
    }

    /// <summary>このアクション文字列が来たら例外を投げる。</summary>
    public string? ThrowOn { get; set; }

    public List<string> Executed { get; } = new();

    public Task ExecuteAsync(string action)
    {
        if (action == ThrowOn)
        {
            throw new InvalidOperationException($"Failed: {action}");
        }

        Executed.Add(action);
        _journal.Add(action);
        return Task.CompletedTask;
    }
}
