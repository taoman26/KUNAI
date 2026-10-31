using System.Collections.ObjectModel;
using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>UI にバインドするログ。件数上限を超えると古い順に自動削除する。</summary>
public interface ILogService
{
    ObservableCollection<LogEntry> Entries { get; }

    void Add(string message);

    void Clear();
}
