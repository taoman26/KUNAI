using System.Diagnostics;
using KUNAI.Core.Abstractions;

namespace KUNAI.Core.Services;

/// <summary><see cref="Process.Start(ProcessStartInfo)"/> による既定の起動実装。</summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    public void Start(ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        using var process = Process.Start(startInfo);
    }
}
