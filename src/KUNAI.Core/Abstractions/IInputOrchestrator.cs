using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>ボタン入力 → レイヤー更新 → マッピング解決 → アクション実行 を仲介する。</summary>
public interface IInputOrchestrator
{
    /// <summary>以降のボタン入力で使うプロファイルを設定する。</summary>
    void SetProfile(Profile profile);

    /// <summary>入力イベントの購読を開始する。</summary>
    void Start();

    /// <summary>入力イベントの購読を解除する。</summary>
    void Stop();
}
