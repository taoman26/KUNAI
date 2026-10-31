using System.Text.Json;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.Tests.Fakes;

/// <summary>メモリ上のプロファイル保存先。保存・読み込み時に複製して、呼び出し側との参照共有を避ける。</summary>
public sealed class FakeProfileRepository : IProfileRepository
{
    private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

    public int InitializeCalls { get; private set; }

    public List<string> SavedNames { get; } = new();

    /// <summary>これらの名前の読み込みは失敗する。</summary>
    public HashSet<string> FailLoad { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FailSave { get; set; }

    public void Seed(Profile profile) => _store[profile.Name] = Serialize(profile);

    public Profile Stored(string name) => Deserialize(_store[Strip(name)]);

    public bool Contains(string name) => _store.ContainsKey(Strip(name));

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        InitializeCalls++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetProfileNamesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(_store.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<Profile> LoadAsync(string name, CancellationToken cancellationToken = default)
    {
        var key = Strip(name);
        if (FailLoad.Contains(key))
        {
            throw new InvalidDataException($"{key} is broken");
        }

        return _store.TryGetValue(key, out var json)
            ? Task.FromResult(Deserialize(json))
            : throw new FileNotFoundException($"Profile '{key}' was not found.");
    }

    public Task SaveAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        if (FailSave)
        {
            throw new IOException("disk full");
        }

        _store[profile.Name] = Serialize(profile);
        SavedNames.Add(profile.Name);
        return Task.CompletedTask;
    }

    private static string Strip(string name) =>
        name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? name[..^5] : name;

    private static string Serialize(Profile profile) => JsonSerializer.Serialize(profile);

    private static Profile Deserialize(string json) => JsonSerializer.Deserialize<Profile>(json)!;
}
