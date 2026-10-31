namespace KUNAI.Platform.Linux.Native;

/// <summary>仮想入力デバイス。テストで差し替えるための境界。</summary>
internal interface IUInputDevice : IDisposable
{
    /// <summary>キーイベントを 1 つ送り、続けて SYN_REPORT を送って確定させる。</summary>
    void Emit(LinuxKeyEvent keyEvent);
}
