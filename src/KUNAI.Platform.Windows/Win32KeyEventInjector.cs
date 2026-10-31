using System.ComponentModel;
using System.Runtime.InteropServices;
using KUNAI.Platform.Windows.Native;

namespace KUNAI.Platform.Windows;

/// <summary>Win32 SendInput でキーイベントを注入する。全イベントを 1 回の呼び出しで送り、他の入力が割り込まないようにする。</summary>
internal sealed class Win32KeyEventInjector : IKeyEventInjector
{
    public void Inject(IReadOnlyList<KeyEvent> events)
    {
        if (events.Count == 0)
        {
            return;
        }

        var inputs = new NativeMethods.INPUT[events.Count];
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var flags = 0u;
            if (e.IsExtended) flags |= NativeMethods.KeyEventFExtendedKey;
            if (e.IsKeyUp) flags |= NativeMethods.KeyEventFKeyUp;

            inputs[i] = new NativeMethods.INPUT
            {
                type = NativeMethods.InputKeyboard,
                U = new NativeMethods.InputUnion
                {
                    ki = new NativeMethods.KEYBDINPUT { wVk = e.VirtualKey, dwFlags = flags }
                }
            };
        }

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            // UIPI(管理者権限ウィンドウへの送出)やデスクトップロック中は失敗する。
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"SendInput injected {sent} of {inputs.Length} events.");
        }
    }
}
