namespace KUNAI.Core.Abstractions;

/// <summary>複数ステップのマクロを順に実行する。"Delay(100)" で待機できる。</summary>
public interface IMacroExecutor
{
    Task ExecuteAsync(IReadOnlyList<string> steps, CancellationToken cancellationToken = default);
}
