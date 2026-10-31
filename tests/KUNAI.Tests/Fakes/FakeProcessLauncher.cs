using System.ComponentModel;
using System.Diagnostics;
using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

/// <summary>起動要求を記録する。<see cref="MissingExecutables"/> に含まれる実行ファイルは「見つからない」として扱う。</summary>
public sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<ProcessStartInfo> Started { get; } = new();

    public HashSet<string> MissingExecutables { get; } = new();

    public void Start(ProcessStartInfo startInfo)
    {
        if (MissingExecutables.Contains(startInfo.FileName))
        {
            throw new Win32Exception(2, $"{startInfo.FileName}: not found");
        }

        Started.Add(startInfo);
    }

    public IEnumerable<string> Commands =>
        Started.Select(s => string.Join(' ', new[] { s.FileName }.Concat(s.ArgumentList)));
}
