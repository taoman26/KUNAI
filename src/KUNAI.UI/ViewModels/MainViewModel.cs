using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Domain;

namespace KUNAI.UI.ViewModels;

/// <summary>メイン画面の ViewModel。接続・プロファイル選択・マッピング編集・ログ表示を扱う。</summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const string ProfileExtension = ".json";
    private const string DefaultProfileName = "default";

    private readonly IWiimoteService _wiimote;
    private readonly ILayerManager _layers;
    private readonly IProfileRepository _repository;
    private readonly IInputOrchestrator _orchestrator;
    private readonly ILogService _log;
    private readonly IUiDispatcher _dispatcher;
    private readonly SemaphoreSlim _profileGate = new(1, 1);

    private Profile? _profile;
    private string? _loadedProfileName;
    private int _displayedLayer;
    private bool _suppressProfileSwitch;
    private bool _suppressLayerSwitch;

    public MainViewModel(
        IWiimoteService wiimote,
        ILayerManager layers,
        IProfileRepository repository,
        IInputOrchestrator orchestrator,
        ILogService log,
        IUiDispatcher dispatcher)
    {
        _wiimote = wiimote ?? throw new ArgumentNullException(nameof(wiimote));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _state = _wiimote.State;
        _batteryPercent = _wiimote.BatteryPercent;
        _activeLayer = _layers.ActiveLayer;

        _wiimote.ConnectionStateChanged += OnConnectionStateChanged;
        _wiimote.BatteryChanged += OnBatteryChanged;
        _layers.LayerChanged += OnLayerChanged;
    }

    public string Title => "KUNAI";

    public string Subtitle => "Programmable Wiimote Macro Pad";

    /// <summary>編集できるレイヤー番号(0〜3)。</summary>
    public IReadOnlyList<int> LayerNumbers { get; } = Enumerable.Range(0, Profile.LayerCount).ToArray();

    /// <summary>プロファイル一覧("coding.json" 形式)。</summary>
    public ObservableCollection<string> ProfileNames { get; } = new();

    /// <summary>編集中レイヤーのマッピング行。</summary>
    public ObservableCollection<MappingRowViewModel> Mappings { get; } = new();

    /// <summary>ログ(最大 1000 件)。</summary>
    public ObservableCollection<LogEntry> Log => _log.Entries;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private ConnectionState _state;

    [ObservableProperty]
    private int _batteryPercent;

    [ObservableProperty]
    private int _activeLayer;

    [ObservableProperty]
    private string? _selectedProfileName;

    [ObservableProperty]
    private int _editingLayer;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private MappingRowViewModel? _selectedMapping;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public string StatusText => State.ToString();

    public string ConnectButtonText => State == ConnectionState.Connected ? "Disconnect" : "Connect";

    /// <summary>起動時の処理。プロファイルを初期化・読み込み、入力の受付を開始する。</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();

            var names = await _repository.GetProfileNamesAsync();
            if (names.Count == 0)
            {
                await _repository.SaveAsync(Profile.CreateEmpty(DefaultProfileName));
                names = await _repository.GetProfileNamesAsync();
            }

            ProfileNames.Clear();
            foreach (var name in names)
            {
                ProfileNames.Add(name + ProfileExtension);
            }

            var initial = ProfileNames.FirstOrDefault(n => string.Equals(n, DefaultProfileName + ProfileExtension,
                                  StringComparison.OrdinalIgnoreCase))
                          ?? ProfileNames.First();

            await _profileGate.WaitAsync();
            try
            {
                SetSelectedProfileSilently(initial);
                await LoadProfileCoreAsync(initial);
            }
            finally
            {
                _profileGate.Release();
            }
        }
        catch (Exception ex)
        {
            ReportError($"Startup failed: {ex.Message}");
        }

        _orchestrator.Start();
    }

    /// <summary>終了時の処理。編集中の内容を保存し、入力を止めて切断する。</summary>
    public async Task ShutdownAsync()
    {
        await _profileGate.WaitAsync();
        try
        {
            await SaveCurrentProfileCoreAsync();
        }
        finally
        {
            _profileGate.Release();
        }

        _orchestrator.Stop();

        try
        {
            if (_wiimote.State != ConnectionState.Disconnected)
            {
                await _wiimote.DisconnectAsync();
            }
        }
        catch (Exception ex)
        {
            ReportError($"Disconnect failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _wiimote.ConnectionStateChanged -= OnConnectionStateChanged;
        _wiimote.BatteryChanged -= OnBatteryChanged;
        _layers.LayerChanged -= OnLayerChanged;
        _profileGate.Dispose();
    }

    private bool CanConnect() => State != ConnectionState.Connecting;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        try
        {
            if (State == ConnectionState.Connected)
            {
                await _wiimote.DisconnectAsync();
            }
            else
            {
                await _wiimote.ConnectAsync();
            }
        }
        catch (Exception ex)
        {
            ReportError($"Connection error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Add()
    {
        if (_profile is null)
        {
            return;
        }

        var used = Mappings.Select(m => m.Button).ToHashSet();
        var next = MappingRowViewModel.AvailableButtons.Where(b => !used.Contains(b)).Cast<WiimoteButton?>().FirstOrDefault();
        if (next is null)
        {
            StatusMessage = "All buttons are already mapped in this layer.";
            return;
        }

        var row = new MappingRowViewModel(next.Value, string.Empty);
        Mappings.Add(row);
        SelectedMapping = row;
        StatusMessage = string.Empty;
    }

    private bool CanRemove() => SelectedMapping is not null;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove()
    {
        if (SelectedMapping is null)
        {
            return;
        }

        var index = Mappings.IndexOf(SelectedMapping);
        Mappings.Remove(SelectedMapping);
        SelectedMapping = Mappings.Count == 0 ? null : Mappings[Math.Min(index, Mappings.Count - 1)];
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_profile is null)
        {
            return;
        }

        await _profileGate.WaitAsync();
        try
        {
            FlushRowsToProfile();

            var error = ValidateProfile(_profile);
            if (error is not null)
            {
                ReportError(error);
                return;
            }

            await _repository.SaveAsync(_profile);
            _orchestrator.SetProfile(_profile);

            var message = $"Saved {_profile.Name}{ProfileExtension}";
            StatusMessage = message;
            _log.Add(message);
        }
        catch (Exception ex)
        {
            ReportError($"Save failed: {ex.Message}");
        }
        finally
        {
            _profileGate.Release();
        }
    }

    partial void OnSelectedProfileNameChanged(string? oldValue, string? newValue)
    {
        if (_suppressProfileSwitch || newValue is null)
        {
            return;
        }

        _ = SwitchProfileAsync(newValue, oldValue);
    }

    partial void OnEditingLayerChanged(int oldValue, int newValue)
    {
        if (_suppressLayerSwitch || _profile is null)
        {
            return;
        }

        FlushRowsToProfile();
        LoadRows(newValue);
    }

    private async Task SwitchProfileAsync(string newName, string? previousName)
    {
        await _profileGate.WaitAsync();
        try
        {
            // 切り替え前に、編集中の内容を自動保存する。
            await SaveCurrentProfileCoreAsync();

            if (!await TryLoadProfileCoreAsync(newName))
            {
                // 読み込みに失敗したら、選択を元に戻して現在のプロファイルを使い続ける。
                SetSelectedProfileSilently(previousName ?? _loadedProfileName);
            }
        }
        finally
        {
            _profileGate.Release();
        }
    }

    private async Task LoadProfileCoreAsync(string name)
    {
        if (!await TryLoadProfileCoreAsync(name))
        {
            _profile = null;
            Mappings.Clear();
        }
    }

    private async Task<bool> TryLoadProfileCoreAsync(string name)
    {
        try
        {
            var profile = await _repository.LoadAsync(name);

            _profile = profile;
            _loadedProfileName = name;
            _orchestrator.SetProfile(profile);

            _suppressLayerSwitch = true;
            EditingLayer = 0;
            _suppressLayerSwitch = false;
            LoadRows(0);

            StatusMessage = string.Empty;
            _log.Add($"Profile {profile.Name} loaded");
            return true;
        }
        catch (Exception ex)
        {
            ReportError($"Failed to load {name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>検証なしで現在のプロファイルを保存する(切り替え時・終了時の自動保存)。</summary>
    private async Task SaveCurrentProfileCoreAsync()
    {
        if (_profile is null)
        {
            return;
        }

        try
        {
            FlushRowsToProfile();
            await _repository.SaveAsync(_profile);
        }
        catch (Exception ex)
        {
            ReportError($"Auto-save failed: {ex.Message}");
        }
    }

    private void LoadRows(int layer)
    {
        _displayedLayer = layer;
        Mappings.Clear();
        SelectedMapping = null;

        if (_profile is null)
        {
            return;
        }

        foreach (var mapping in _profile.GetOrAddLayer(layer).Mappings)
        {
            Mappings.Add(MappingRowViewModel.FromMapping(mapping));
        }
    }

    /// <summary>表示中の行をプロファイルへ書き戻す。リストごと差し替えるので、読み取り側が途中状態を見ることはない。</summary>
    private void FlushRowsToProfile()
    {
        if (_profile is null)
        {
            return;
        }

        _profile.GetOrAddLayer(_displayedLayer).Mappings = Mappings.Select(r => r.ToMapping()).ToList();
    }

    private static string? ValidateProfile(Profile profile)
    {
        foreach (var layer in profile.Layers)
        {
            var seen = new HashSet<WiimoteButton>();
            foreach (var mapping in layer.Mappings)
            {
                if (!seen.Add(mapping.Button))
                {
                    return $"Layer {layer.LayerNumber}: {mapping.Button} is mapped more than once.";
                }

                var error = ActionTextCodec.Validate(ActionTextCodec.Format(mapping));
                if (error is not null)
                {
                    return $"Layer {layer.LayerNumber}, {mapping.Button}: {error}";
                }
            }
        }

        return null;
    }

    private void SetSelectedProfileSilently(string? name)
    {
        _suppressProfileSwitch = true;
        try
        {
            SelectedProfileName = name;
        }
        finally
        {
            _suppressProfileSwitch = false;
        }
    }

    private void ReportError(string message)
    {
        StatusMessage = message;
        _log.Add(message);
    }

    // バックグラウンド(Wiimote の入力スレッド)から呼ばれるため、UI スレッドへ渡して更新する。
    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        _dispatcher.Post(() =>
        {
            State = state;
            if (state == ConnectionState.Disconnected)
            {
                BatteryPercent = 0;
            }
        });

    private void OnBatteryChanged(object? sender, int percent) => _dispatcher.Post(() => BatteryPercent = percent);

    private void OnLayerChanged(object? sender, int layer) => _dispatcher.Post(() => ActiveLayer = layer);
}
