namespace KUNAI.Core.Abstractions;

/// <summary>OS へキー入力を送出する。プラットフォーム層が実装する。</summary>
public interface ISyntheticKeyboard
{
    /// <summary>"Ctrl+Shift+T" 形式のショートカットを送出する。不正な文字列は <see cref="FormatException"/>。</summary>
    Task SendShortcutAsync(string shortcut);
}
