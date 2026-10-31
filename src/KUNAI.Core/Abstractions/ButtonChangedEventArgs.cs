using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>ボタンの押下/解放イベントの引数。</summary>
public class ButtonChangedEventArgs : EventArgs
{
    public ButtonChangedEventArgs(WiimoteButton button)
    {
        Button = button;
    }

    public WiimoteButton Button { get; }
}
