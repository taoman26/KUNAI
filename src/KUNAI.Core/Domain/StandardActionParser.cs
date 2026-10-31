namespace KUNAI.Core.Domain;

/// <summary>文字列から <see cref="StandardAction"/> を名前で解釈する。</summary>
public static class StandardActionParser
{
    /// <summary>
    /// 名前の一致(大文字小文字は区別しない)だけで判定する。
    /// <see cref="Enum.TryParse{TEnum}(string?, bool, out TEnum)"/> は "1" を数値として受理し
    /// (StandardAction)1 にしてしまい、ショートカット "1" と衝突するため使わない。
    /// </summary>
    public static bool TryParse(string? text, out StandardAction action)
    {
        var trimmed = text?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            foreach (var name in Enum.GetNames<StandardAction>())
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    action = Enum.Parse<StandardAction>(name);
                    return true;
                }
            }
        }

        action = default;
        return false;
    }
}
