using System.Text.Json.Serialization;

namespace KUNAI.Core.Domain;

/// <summary>1 つのボタンに割り当てるアクション。</summary>
public class ButtonMapping
{
    public WiimoteButton Button { get; set; }

    /// <summary>ショートカット("Ctrl+C")または標準アクション名("LaunchBrowser")。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// マクロのステップ列(例: ["Ctrl+C", "Delay(100)", "Ctrl+V"])。
    /// 非 null かつ要素がある場合は <see cref="Action"/> より優先される。
    /// </summary>
    public List<string>? Macro { get; set; }

    /// <summary>マクロとして実行すべきか。(JSON には出力しない)</summary>
    [JsonIgnore]
    public bool IsMacro => Macro is { Count: > 0 };
}
