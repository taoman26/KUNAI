namespace KUNAI.Core.Abstractions;

/// <summary>待機の抽象。テストで実時間を待たないために差し替え可能にする。</summary>
public interface IDelayProvider
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}
