using CommunityToolkit.Mvvm.ComponentModel;
using KUNAI.Core.Domain;

namespace KUNAI.UI.ViewModels;

/// <summary>Mappings 表の 1 行(ボタンとアクション)。</summary>
public partial class MappingRowViewModel : ObservableObject
{
    [ObservableProperty]
    private WiimoteButton _button;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _actionText;

    /// <summary>割り当てできるボタン。B と Home はレイヤー切替専用のため含めない。</summary>
    public static IReadOnlyList<WiimoteButton> AvailableButtons { get; } =
        Enum.GetValues<WiimoteButton>().Where(b => b is not (WiimoteButton.B or WiimoteButton.Home)).ToArray();

    public MappingRowViewModel(WiimoteButton button, string actionText)
    {
        _button = button;
        _actionText = actionText;
    }

    /// <summary>アクション文字列の問題点。問題なければ null。</summary>
    public string? Error => ActionTextCodec.Validate(ActionText);

    public bool HasError => Error is not null;

    public static MappingRowViewModel FromMapping(ButtonMapping mapping) =>
        new(mapping.Button, ActionTextCodec.Format(mapping));

    public ButtonMapping ToMapping()
    {
        var mapping = new ButtonMapping { Button = Button };
        ActionTextCodec.Apply(ActionText, mapping);
        return mapping;
    }
}
