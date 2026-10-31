namespace KUNAI.Platform.Windows.Native;

/// <summary>送出するキーイベント 1 つ(押下または解放)。</summary>
internal readonly record struct KeyEvent(ushort VirtualKey, bool IsExtended, bool IsKeyUp);
