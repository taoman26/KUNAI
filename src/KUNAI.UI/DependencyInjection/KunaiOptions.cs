namespace KUNAI.UI.DependencyInjection;

/// <summary>アプリ全体の設定。null の項目は既定値を使う。</summary>
/// <param name="ProfilesDirectory">プロファイルの保存先。null ならユーザー設定領域(~/.config/KUNAI/profiles または %APPDATA%\KUNAI\profiles)。</param>
/// <param name="BundledProfilesDirectory">初回起動時にコピーする同梱プロファイルの場所。null なら実行ファイルと同じ場所の profiles/。</param>
public sealed record KunaiOptions(
    string? ProfilesDirectory = null,
    string? BundledProfilesDirectory = null);
