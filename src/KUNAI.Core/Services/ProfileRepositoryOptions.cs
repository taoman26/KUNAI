namespace KUNAI.Core.Services;

/// <summary>ProfileRepository の保存先設定。</summary>
/// <param name="ProfilesDirectory">プロファイル JSON を読み書きするディレクトリ。</param>
/// <param name="DefaultsDirectory">初回起動時にコピーする同梱プロファイルの場所。null なら何もコピーしない。</param>
public sealed record ProfileRepositoryOptions(string ProfilesDirectory, string? DefaultsDirectory = null)
{
    /// <summary>
    /// ユーザー設定領域を保存先にした既定の設定を作る。
    /// Linux: ~/.config/KUNAI/profiles、Windows: %APPDATA%\KUNAI\profiles。
    /// </summary>
    public static ProfileRepositoryOptions CreateDefault(string? defaultsDirectory)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new ProfileRepositoryOptions(Path.Combine(appData, "KUNAI", "profiles"), defaultsDirectory);
    }
}
