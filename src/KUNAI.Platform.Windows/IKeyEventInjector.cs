using KUNAI.Platform.Windows.Native;

namespace KUNAI.Platform.Windows;

/// <summary>キーイベント列を OS へ注入する。テストで差し替えるための境界。</summary>
internal interface IKeyEventInjector
{
    void Inject(IReadOnlyList<KeyEvent> events);
}
