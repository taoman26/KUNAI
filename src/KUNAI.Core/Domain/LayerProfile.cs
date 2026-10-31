namespace KUNAI.Core.Domain;

/// <summary>1 レイヤー分のボタン割り当て。</summary>
public class LayerProfile
{
    public int LayerNumber { get; set; }

    public List<ButtonMapping> Mappings { get; set; } = new();
}
