namespace KUNAI.Core.Domain;

/// <summary>ログ 1 行分。表示形式は "[12:00:01] Connected"。</summary>
public sealed record LogEntry(DateTime Timestamp, string Message)
{
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Message}";
}
