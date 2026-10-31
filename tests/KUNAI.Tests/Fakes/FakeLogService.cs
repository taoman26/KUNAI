using System.Collections.ObjectModel;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Tests.Fakes;

public sealed class FakeLogService : ILogService
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public List<string> Messages => Entries.Select(e => e.Message).ToList();

    public void Add(string message) => Entries.Add(new LogEntry(DateTime.Now, message));

    public void Clear() => Entries.Clear();
}
