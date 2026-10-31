using System.Diagnostics;

namespace KUNAI.Core.Abstractions;

/// <summary>外部プロセスの起動。テストで実際のプロセスを起動しないための境界。</summary>
public interface IProcessLauncher
{
    /// <summary>
    /// プロセスを起動して待たずに戻る。実行ファイルが見つからない場合は
    /// <see cref="System.ComponentModel.Win32Exception"/>。
    /// </summary>
    void Start(ProcessStartInfo startInfo);
}
