using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Core.Services;

/// <summary>
/// QMK 風のモメンタリーレイヤー管理。
/// B 押下中 = Layer1、Home 押下中 = Layer2、両方 = Layer3。離すと残りの押下状態に応じたレイヤーへ戻る。
/// 状態は「B / Home が押されているか」から毎回導出するため、押す順序や離す順序に依存しない。
/// </summary>
public sealed class LayerManager : ILayerManager
{
    private const int BLayerBit = 1;
    private const int HomeLayerBit = 2;

    private readonly object _gate = new();
    private bool _bHeld;
    private bool _homeHeld;
    private int _activeLayer;

    public int ActiveLayer
    {
        get
        {
            lock (_gate)
            {
                return _activeLayer;
            }
        }
    }

    public event EventHandler<int>? LayerChanged;

    public void OnButtonPressed(WiimoteButton button) => Update(button, pressed: true);

    public void OnButtonReleased(WiimoteButton button) => Update(button, pressed: false);

    public void Reset()
    {
        int? changedTo = null;
        lock (_gate)
        {
            _bHeld = false;
            _homeHeld = false;
            changedTo = Recompute();
        }

        RaiseIfChanged(changedTo);
    }

    private void Update(WiimoteButton button, bool pressed)
    {
        int? changedTo;
        lock (_gate)
        {
            switch (button)
            {
                case WiimoteButton.B:
                    _bHeld = pressed;
                    break;
                case WiimoteButton.Home:
                    _homeHeld = pressed;
                    break;
                default:
                    return; // レイヤーに影響しないボタン
            }

            changedTo = Recompute();
        }

        RaiseIfChanged(changedTo);
    }

    /// <summary>ロック内で呼ぶ。レイヤーが変わった場合のみ新しい値を返す。</summary>
    private int? Recompute()
    {
        var layer = (_bHeld ? BLayerBit : 0) | (_homeHeld ? HomeLayerBit : 0);
        if (layer == _activeLayer)
        {
            return null;
        }

        _activeLayer = layer;
        return layer;
    }

    private void RaiseIfChanged(int? layer)
    {
        if (layer is { } value)
        {
            LayerChanged?.Invoke(this, value);
        }
    }
}
