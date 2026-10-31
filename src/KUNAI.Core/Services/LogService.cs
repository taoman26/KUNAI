using System.Collections.ObjectModel;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Core.Services;

/// <summary>
/// UI にバインドするログ。最大件数(既定 1000)を超えたら古いものから自動削除する。
/// どのスレッドからでも <see cref="Add"/> でき、コレクションの更新は <see cref="IUiDispatcher"/> 経由で行う。
/// </summary>
public sealed class LogService : ILogService
{
    public const int DefaultMaxEntries = 1000;

    private readonly IUiDispatcher _dispatcher;
    private readonly int _maxEntries;
    private readonly TimeProvider _timeProvider;

    public LogService(IUiDispatcher dispatcher)
        : this(dispatcher, DefaultMaxEntries, TimeProvider.System)
    {
    }

    public LogService(IUiDispatcher dispatcher, int maxEntries, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntries, 1);

        _dispatcher = dispatcher;
        _maxEntries = maxEntries;
        _timeProvider = timeProvider;
    }

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Add(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // 時刻は呼び出し時点で確定させる(UI スレッドでの反映が遅れても、ログの時刻は発生時刻になる)。
        var entry = new LogEntry(_timeProvider.GetLocalNow().DateTime, message);

        _dispatcher.Post(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > _maxEntries)
            {
                Entries.RemoveAt(0);
            }
        });
    }

    public void Clear() => _dispatcher.Post(Entries.Clear);
}
