namespace KUNAI.Core.Domain;

/// <summary>名前付きのキーマップ一式。</summary>
public class Profile
{
    /// <summary>レイヤー数(Layer0〜Layer3)。</summary>
    public const int LayerCount = 4;

    public string Name { get; set; } = string.Empty;

    public List<LayerProfile> Layers { get; set; } = new();

    /// <summary>指定レイヤー・ボタンの割り当てを返す。無ければ null。</summary>
    public ButtonMapping? FindMapping(int layer, WiimoteButton button)
    {
        var layerProfile = Layers.FirstOrDefault(l => l.LayerNumber == layer);
        return layerProfile?.Mappings.FirstOrDefault(m => m.Button == button);
    }

    /// <summary>指定レイヤーを返す。無ければ空のレイヤーを追加して返す。</summary>
    public LayerProfile GetOrAddLayer(int layer)
    {
        var existing = Layers.FirstOrDefault(l => l.LayerNumber == layer);
        if (existing is not null)
        {
            return existing;
        }

        var created = new LayerProfile { LayerNumber = layer };
        Layers.Add(created);
        Layers.Sort((a, b) => a.LayerNumber.CompareTo(b.LayerNumber));
        return created;
    }

    /// <summary>全レイヤーを空で持つプロファイルを作る。</summary>
    public static Profile CreateEmpty(string name)
    {
        var profile = new Profile { Name = name };
        for (var i = 0; i < LayerCount; i++)
        {
            profile.Layers.Add(new LayerProfile { LayerNumber = i });
        }

        return profile;
    }
}
