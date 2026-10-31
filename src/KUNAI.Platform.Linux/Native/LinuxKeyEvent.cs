namespace KUNAI.Platform.Linux.Native;

/// <summary>送出するキーイベント 1 つ(evdev のキーコードと押下/解放)。</summary>
internal readonly record struct LinuxKeyEvent(ushort Code, bool IsDown);
