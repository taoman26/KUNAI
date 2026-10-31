using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>押下中のボタンからアクティブなレイヤー(0〜3)を管理する。</summary>
public interface ILayerManager
{
    int ActiveLayer { get; }

    void OnButtonPressed(WiimoteButton button);

    void OnButtonReleased(WiimoteButton button);

    /// <summary>押下状態を破棄して Layer0 に戻す(切断時など)。</summary>
    void Reset();

    /// <summary>アクティブレイヤーが変化したとき。引数は新しいレイヤー番号。</summary>
    event EventHandler<int> LayerChanged;
}
