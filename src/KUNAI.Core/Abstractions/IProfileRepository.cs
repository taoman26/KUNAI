using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>プロファイルの JSON 永続化。</summary>
public interface IProfileRepository
{
    /// <summary>保存先が空なら同梱の初期プロファイルをコピーする。</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>保存済みプロファイル名(拡張子なし、昇順)。</summary>
    Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default);

    Task<Profile> LoadAsync(string name, CancellationToken cancellationToken = default);

    Task SaveAsync(Profile profile, CancellationToken cancellationToken = default);
}
