using KUNAI.Core.Domain;
using KUNAI.Core.Services;
using KUNAI.Tests.Fakes;
using KUNAI.UI.ViewModels;

namespace KUNAI.Tests.UI;

public class MainViewModelTests
{
    private readonly FakeWiimoteService _wiimote = new();
    private readonly LayerManager _layers = new();
    private readonly FakeProfileRepository _repository = new();
    private readonly FakeInputOrchestrator _orchestrator = new();
    private readonly FakeLogService _log = new();
    private readonly QueueingUiDispatcher _queue = new();

    public MainViewModelTests()
    {
        _repository.Seed(CreateProfile("default"));
        _repository.Seed(CreateProfile("coding", a0: "Ctrl+S"));
        _repository.Seed(CreateProfile("browser", a0: "Ctrl+T"));
    }

    private static Profile CreateProfile(string name, string a0 = "Enter")
    {
        var profile = Profile.CreateEmpty(name);
        profile.Layers[0].Mappings.Add(new ButtonMapping { Button = WiimoteButton.A, Action = a0 });
        profile.Layers[0].Mappings.Add(new ButtonMapping { Button = WiimoteButton.One, Action = "Ctrl+C" });
        profile.Layers[0].Mappings.Add(new ButtonMapping { Button = WiimoteButton.Two, Action = "Ctrl+V" });
        profile.Layers[1].Mappings.Add(new ButtonMapping { Button = WiimoteButton.A, Action = "Ctrl+Z" });
        return profile;
    }

    private MainViewModel Create(bool immediate = true) =>
        new(_wiimote, _layers, _repository, _orchestrator, _log,
            immediate ? new ImmediateUiDispatcher() : _queue);

    private async Task<MainViewModel> CreateInitializedAsync()
    {
        var vm = Create();
        await vm.InitializeAsync();
        return vm;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Timed out waiting for: {what}");
            await Task.Delay(5);
        }
    }

    // ---- 起動 ----

    [Fact]
    public async Task Initialize_LoadsProfileNamesWithExtension_AndSelectsDefault()
    {
        var vm = await CreateInitializedAsync();

        Assert.Equal(new[] { "browser.json", "coding.json", "default.json" }, vm.ProfileNames);
        Assert.Equal("default.json", vm.SelectedProfileName);
        Assert.Equal(1, _repository.InitializeCalls);
    }

    [Fact]
    public async Task Initialize_ShowsLayer0Mappings_AndHandsProfileToOrchestrator_AndStartsIt()
    {
        var vm = await CreateInitializedAsync();

        Assert.Equal(new[] { WiimoteButton.A, WiimoteButton.One, WiimoteButton.Two }, vm.Mappings.Select(m => m.Button));
        Assert.Equal(new[] { "Enter", "Ctrl+C", "Ctrl+V" }, vm.Mappings.Select(m => m.ActionText));
        Assert.Equal("default", Assert.Single(_orchestrator.Profiles).Name);
        Assert.Equal(1, _orchestrator.StartCalls);
        Assert.Contains("Profile default loaded", _log.Messages);
    }

    [Fact]
    public async Task Initialize_WithoutDefault_SelectsTheFirstProfile()
    {
        var repository = new FakeProfileRepository();
        repository.Seed(CreateProfile("writing"));
        repository.Seed(CreateProfile("browser"));
        var vm = new MainViewModel(_wiimote, _layers, repository, _orchestrator, _log, new ImmediateUiDispatcher());

        await vm.InitializeAsync();

        Assert.Equal("browser.json", vm.SelectedProfileName);
    }

    [Fact]
    public async Task Initialize_WithNoProfiles_CreatesAnEmptyDefault()
    {
        var repository = new FakeProfileRepository();
        var vm = new MainViewModel(_wiimote, _layers, repository, _orchestrator, _log, new ImmediateUiDispatcher());

        await vm.InitializeAsync();

        Assert.True(repository.Contains("default"));
        Assert.Equal(new[] { "default.json" }, vm.ProfileNames);
        Assert.Empty(vm.Mappings);
    }

    [Fact]
    public async Task Initialize_WhenLoadFails_ReportsError_ButStillStartsInput()
    {
        _repository.FailLoad.Add("default");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Empty(vm.Mappings);
        Assert.Contains(_log.Messages, m => m.StartsWith("Failed to load default.json"));
        Assert.Contains("Failed to load", vm.StatusMessage);
        Assert.Equal(1, _orchestrator.StartCalls);
    }

    [Fact]
    public void Header_ShowsTitleAndSubtitle()
    {
        var vm = Create();

        Assert.Equal("KUNAI", vm.Title);
        Assert.Equal("Programmable Wiimote Macro Pad", vm.Subtitle);
        Assert.Equal(new[] { 0, 1, 2, 3 }, vm.LayerNumbers);
    }

    // ---- 接続 ----

    [Fact]
    public async Task Connect_TogglesBetweenConnectAndDisconnect()
    {
        var vm = await CreateInitializedAsync();
        Assert.Equal("Connect", vm.ConnectButtonText);
        Assert.Equal("Disconnected", vm.StatusText);

        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(1, _wiimote.ConnectCalls);
        Assert.Equal(ConnectionState.Connected, vm.State);
        Assert.Equal("Disconnect", vm.ConnectButtonText);
        Assert.Equal("Connected", vm.StatusText);

        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(1, _wiimote.DisconnectCalls);
        Assert.Equal(ConnectionState.Disconnected, vm.State);
        Assert.Equal("Connect", vm.ConnectButtonText);
    }

    [Fact]
    public async Task Connect_Failure_IsLoggedAndShown_NotThrown()
    {
        _wiimote.ConnectException = new TimeoutException("No Wii Remote was found");
        var vm = await CreateInitializedAsync();

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Contains("Connection error: No Wii Remote was found", _log.Messages);
        Assert.Equal("Connection error: No Wii Remote was found", vm.StatusMessage);
    }

    [Fact]
    public async Task ConnectCommand_IsDisabledWhileConnecting()
    {
        var vm = await CreateInitializedAsync();
        var changed = 0;
        vm.ConnectCommand.CanExecuteChanged += (_, _) => changed++;

        _wiimote.SetState(ConnectionState.Connecting);
        Assert.False(vm.ConnectCommand.CanExecute(null));
        Assert.Equal("Connecting", vm.StatusText);

        _wiimote.SetState(ConnectionState.Connected);
        Assert.True(vm.ConnectCommand.CanExecute(null));
        Assert.True(changed >= 2);
    }

    // ---- デバイス状態の反映 ----

    [Fact]
    public async Task DeviceEvents_UpdateTheViewModel_OnlyThroughTheUiDispatcher()
    {
        var vm = Create(immediate: false);

        _wiimote.SetState(ConnectionState.Connected);
        _wiimote.SetBattery(82);
        _layers.OnButtonPressed(WiimoteButton.B);

        Assert.Equal(ConnectionState.Disconnected, vm.State); // まだ UI スレッドに渡っていない
        Assert.Equal(0, vm.BatteryPercent);
        Assert.Equal(0, vm.ActiveLayer);

        _queue.RunAll();

        Assert.Equal(ConnectionState.Connected, vm.State);
        Assert.Equal(82, vm.BatteryPercent);
        Assert.Equal(1, vm.ActiveLayer);
        await Task.CompletedTask;
    }

    [Fact]
    public void Disconnect_ResetsBatteryDisplay()
    {
        var vm = Create();
        _wiimote.SetState(ConnectionState.Connected);
        _wiimote.SetBattery(50);

        _wiimote.SetState(ConnectionState.Disconnected);

        Assert.Equal(0, vm.BatteryPercent);
    }

    [Fact]
    public void LayerChanges_AreReflected()
    {
        var vm = Create();

        _layers.OnButtonPressed(WiimoteButton.B);
        _layers.OnButtonPressed(WiimoteButton.Home);
        Assert.Equal(3, vm.ActiveLayer);

        _layers.OnButtonReleased(WiimoteButton.B);
        _layers.OnButtonReleased(WiimoteButton.Home);
        Assert.Equal(0, vm.ActiveLayer);
    }

    [Fact]
    public void InitialState_ReflectsTheCurrentDevice()
    {
        _wiimote.SetState(ConnectionState.Connected);
        _wiimote.SetBattery(40);
        _layers.OnButtonPressed(WiimoteButton.Home);

        var vm = Create();

        Assert.Equal(ConnectionState.Connected, vm.State);
        Assert.Equal(40, vm.BatteryPercent);
        Assert.Equal(2, vm.ActiveLayer);
    }

    // ---- レイヤー編集 ----

    [Fact]
    public async Task EditingLayer_SwitchesTheVisibleRows()
    {
        var vm = await CreateInitializedAsync();

        vm.EditingLayer = 1;

        Assert.Equal(new[] { "Ctrl+Z" }, vm.Mappings.Select(m => m.ActionText));

        vm.EditingLayer = 3;
        Assert.Empty(vm.Mappings);
    }

    [Fact]
    public async Task EditsSurviveSwitchingLayersBackAndForth()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[0].ActionText = "Space";

        vm.EditingLayer = 1;
        vm.Mappings[0].ActionText = "Ctrl+Y";
        vm.EditingLayer = 0;

        Assert.Equal("Space", vm.Mappings[0].ActionText);
        vm.EditingLayer = 1;
        Assert.Equal("Ctrl+Y", vm.Mappings[0].ActionText);
    }

    // ---- Add / Remove ----

    [Fact]
    public async Task Add_AppendsTheFirstUnusedButton_AndSelectsIt()
    {
        var vm = await CreateInitializedAsync(); // A, One, Two が使用済み

        vm.AddCommand.Execute(null);

        var row = vm.Mappings[^1];
        Assert.Equal(WiimoteButton.Plus, row.Button);
        Assert.Equal(string.Empty, row.ActionText);
        Assert.Same(row, vm.SelectedMapping);
    }

    [Fact]
    public async Task Add_NeverOffersLayerButtons_AndStopsWhenAllAreUsed()
    {
        var vm = await CreateInitializedAsync();

        for (var i = 0; i < 20; i++) vm.AddCommand.Execute(null);

        Assert.DoesNotContain(vm.Mappings, m => m.Button is WiimoteButton.B or WiimoteButton.Home);
        Assert.Equal(MappingRowViewModel.AvailableButtons.Count, vm.Mappings.Count);
        Assert.Equal(vm.Mappings.Count, vm.Mappings.Select(m => m.Button).Distinct().Count());
        Assert.Equal("All buttons are already mapped in this layer.", vm.StatusMessage);
    }

    [Fact]
    public void Add_WithoutProfile_DoesNothing()
    {
        var vm = Create();

        vm.AddCommand.Execute(null);

        Assert.Empty(vm.Mappings);
    }

    [Fact]
    public async Task Remove_DeletesTheSelectedRow_AndSelectsItsNeighbour()
    {
        var vm = await CreateInitializedAsync();
        vm.SelectedMapping = vm.Mappings[1];

        vm.RemoveCommand.Execute(null);

        Assert.Equal(new[] { WiimoteButton.A, WiimoteButton.Two }, vm.Mappings.Select(m => m.Button));
        Assert.Same(vm.Mappings[1], vm.SelectedMapping);

        vm.SelectedMapping = vm.Mappings[1];
        vm.RemoveCommand.Execute(null);
        Assert.Same(vm.Mappings[0], vm.SelectedMapping);

        vm.RemoveCommand.Execute(null);
        Assert.Empty(vm.Mappings);
        Assert.Null(vm.SelectedMapping);
    }

    [Fact]
    public async Task RemoveCommand_IsEnabledOnlyWithASelection()
    {
        var vm = await CreateInitializedAsync();
        Assert.False(vm.RemoveCommand.CanExecute(null));

        vm.SelectedMapping = vm.Mappings[0];
        Assert.True(vm.RemoveCommand.CanExecute(null));

        vm.SelectedMapping = null;
        Assert.False(vm.RemoveCommand.CanExecute(null));
    }

    // ---- Save ----

    [Fact]
    public async Task Save_PersistsEdits_NotifiesOrchestrator_AndReports()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[0].ActionText = "Space";
        var profilesBefore = _orchestrator.Profiles.Count;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Space", _repository.Stored("default").FindMapping(0, WiimoteButton.A)!.Action);
        Assert.Equal(profilesBefore + 1, _orchestrator.Profiles.Count);
        Assert.Equal("Saved default.json", vm.StatusMessage);
        Assert.Contains("Saved default.json", _log.Messages);
    }

    [Fact]
    public async Task Save_StoresSemicolonTextAsMacro()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[0].ActionText = "Ctrl+S; Delay(300); Alt+Tab";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = _repository.Stored("default").FindMapping(0, WiimoteButton.A)!;
        Assert.Equal(new[] { "Ctrl+S", "Delay(300)", "Alt+Tab" }, saved.Macro!);
        Assert.Equal(string.Empty, saved.Action);
    }

    [Fact]
    public async Task Save_StoresAddedAndRemovedRows()
    {
        var vm = await CreateInitializedAsync();
        vm.SelectedMapping = vm.Mappings[0];
        vm.RemoveCommand.Execute(null);
        vm.AddCommand.Execute(null);
        vm.Mappings[^1].ActionText = "F5";

        await vm.SaveCommand.ExecuteAsync(null);

        var layer0 = _repository.Stored("default").Layers.Single(l => l.LayerNumber == 0);
        Assert.Equal(new[] { WiimoteButton.One, WiimoteButton.Two, WiimoteButton.A }, layer0.Mappings.Select(m => m.Button));
        Assert.Equal("F5", layer0.Mappings[^1].Action);
    }

    [Fact]
    public async Task Save_WithUnknownAction_IsRejected_AndNothingIsWritten()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[0].ActionText = "Nonsense";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_repository.SavedNames);
        Assert.Contains("Unknown action: 'Nonsense'", vm.StatusMessage);
        Assert.Contains(_log.Messages, m => m.Contains("Nonsense"));
        Assert.Equal("Enter", _repository.Stored("default").FindMapping(0, WiimoteButton.A)!.Action);
    }

    [Fact]
    public async Task Save_ValidatesEveryLayer_NotOnlyTheVisibleOne()
    {
        var vm = await CreateInitializedAsync();
        vm.EditingLayer = 1;
        vm.Mappings[0].ActionText = "Bogus";
        vm.EditingLayer = 0; // 不正な内容は別レイヤーに隠れている

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_repository.SavedNames);
        Assert.Contains("Layer 1", vm.StatusMessage);
        Assert.Contains("Bogus", vm.StatusMessage);
    }

    [Fact]
    public async Task Save_WithDuplicateButtons_IsRejected()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[1].Button = WiimoteButton.A; // A が 2 行になる

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_repository.SavedNames);
        Assert.Contains("A is mapped more than once", vm.StatusMessage);
    }

    [Fact]
    public async Task Save_WhenRepositoryFails_ShowsTheError()
    {
        var vm = await CreateInitializedAsync();
        _repository.FailSave = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Save failed: disk full", vm.StatusMessage);
    }

    [Fact]
    public void Save_WithoutProfile_DoesNothing()
    {
        var vm = Create();

        vm.SaveCommand.Execute(null);

        Assert.Empty(_repository.SavedNames);
    }

    // ---- プロファイル切替 ----

    [Fact]
    public async Task SwitchingProfile_AutoSavesTheCurrentOne_ThenLoadsTheNewOne()
    {
        var vm = await CreateInitializedAsync();
        vm.Mappings[0].ActionText = "Space";

        vm.SelectedProfileName = "coding.json";
        await WaitUntilAsync(() => vm.Mappings.Count > 0 && vm.Mappings[0].ActionText == "Ctrl+S", "coding loaded");

        Assert.Equal("Space", _repository.Stored("default").FindMapping(0, WiimoteButton.A)!.Action);
        Assert.Equal("coding", _orchestrator.Profiles[^1].Name);
        Assert.Contains("Profile coding loaded", _log.Messages);
    }

    [Fact]
    public async Task SwitchingProfile_ResetsTheEditingLayerToZero()
    {
        var vm = await CreateInitializedAsync();
        vm.EditingLayer = 1;

        vm.SelectedProfileName = "coding.json";
        await WaitUntilAsync(() => _orchestrator.Profiles[^1].Name == "coding", "coding loaded");

        Assert.Equal(0, vm.EditingLayer);
        Assert.Equal("Ctrl+S", vm.Mappings[0].ActionText);
    }

    [Fact]
    public async Task SwitchingToABrokenProfile_RevertsTheSelection_AndKeepsTheCurrentOne()
    {
        _repository.Seed(CreateProfile("broken"));
        _repository.FailLoad.Add("broken");
        var vm = await CreateInitializedAsync();

        vm.SelectedProfileName = "broken.json";
        await WaitUntilAsync(() => vm.StatusMessage.StartsWith("Failed to load"), "load failure reported");

        Assert.Equal("default.json", vm.SelectedProfileName);
        Assert.Equal("Enter", vm.Mappings[0].ActionText);
        Assert.Equal("default", _orchestrator.Profiles[^1].Name);
    }

    // ---- 終了 ----

    [Fact]
    public async Task Shutdown_AutoSavesEdits_EvenIfTheyAreInvalid_StopsInput_AndDisconnects()
    {
        var vm = await CreateInitializedAsync();
        await vm.ConnectCommand.ExecuteAsync(null);
        vm.Mappings[0].ActionText = "WorkInProgress"; // 保存ボタンでは弾かれる内容でも、終了時は失わない

        await vm.ShutdownAsync();

        Assert.Equal("WorkInProgress", _repository.Stored("default").FindMapping(0, WiimoteButton.A)!.Action);
        Assert.Equal(1, _orchestrator.StopCalls);
        Assert.Equal(1, _wiimote.DisconnectCalls);
    }

    [Fact]
    public async Task Shutdown_WhenAlreadyDisconnected_DoesNotDisconnectAgain()
    {
        var vm = await CreateInitializedAsync();

        await vm.ShutdownAsync();

        Assert.Equal(0, _wiimote.DisconnectCalls);
    }

    [Fact]
    public async Task Shutdown_WhenSaveFails_StillStopsAndDisconnects()
    {
        var vm = await CreateInitializedAsync();
        await vm.ConnectCommand.ExecuteAsync(null);
        _repository.FailSave = true;

        await vm.ShutdownAsync();

        Assert.Contains(_log.Messages, m => m.StartsWith("Auto-save failed"));
        Assert.Equal(1, _orchestrator.StopCalls);
        Assert.Equal(1, _wiimote.DisconnectCalls);
    }

    [Fact]
    public async Task Shutdown_WithoutProfile_DoesNotSaveAnything()
    {
        _repository.FailLoad.Add("default");
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ShutdownAsync();

        Assert.Empty(_repository.SavedNames);
    }

    [Fact]
    public void Dispose_UnsubscribesFromDeviceEvents()
    {
        var vm = Create();
        vm.Dispose();

        _wiimote.SetState(ConnectionState.Connected);
        _wiimote.SetBattery(70);
        _layers.OnButtonPressed(WiimoteButton.B);

        Assert.Equal(ConnectionState.Disconnected, vm.State);
        Assert.Equal(0, vm.BatteryPercent);
        Assert.Equal(0, vm.ActiveLayer);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var d = new ImmediateUiDispatcher();
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(null!, _layers, _repository, _orchestrator, _log, d));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_wiimote, null!, _repository, _orchestrator, _log, d));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_wiimote, _layers, null!, _orchestrator, _log, d));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_wiimote, _layers, _repository, null!, _log, d));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_wiimote, _layers, _repository, _orchestrator, null!, d));
        Assert.Throws<ArgumentNullException>(() => new MainViewModel(_wiimote, _layers, _repository, _orchestrator, _log, null!));
    }

    // ---- 行 ----

    [Fact]
    public void MappingRow_ReportsErrorsForInvalidText_AndNotifiesChanges()
    {
        var row = new MappingRowViewModel(WiimoteButton.A, "Ctrl+C");
        var raised = new List<string?>();
        row.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.False(row.HasError);

        row.ActionText = "Nonsense";

        Assert.True(row.HasError);
        Assert.Contains("Nonsense", row.Error);
        Assert.Contains(nameof(MappingRowViewModel.Error), raised);
        Assert.Contains(nameof(MappingRowViewModel.HasError), raised);
    }

    [Fact]
    public void MappingRow_RoundTripsThroughTheDomainMapping()
    {
        var macro = new ButtonMapping
        {
            Button = WiimoteButton.Plus,
            Macro = new List<string> { "Ctrl+S", "Delay(300)", "Alt+Tab" }
        };

        var row = MappingRowViewModel.FromMapping(macro);
        var back = row.ToMapping();

        Assert.Equal("Ctrl+S; Delay(300); Alt+Tab", row.ActionText);
        Assert.Equal(WiimoteButton.Plus, back.Button);
        Assert.Equal(macro.Macro, back.Macro);
    }
}
