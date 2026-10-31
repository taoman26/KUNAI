using KUNAI.Core.Abstractions;
using KUNAI.Core.Services;
using KUNAI.Platform.Linux;
using KUNAI.Platform.Windows;
using KUNAI.UI.DependencyInjection;
using KUNAI.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KUNAI.Tests.UI;

public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _profilesDirectory =
        Path.Combine(Path.GetTempPath(), "kunai-di-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_profilesDirectory))
        {
            Directory.Delete(_profilesDirectory, recursive: true);
        }
    }

    private ServiceProvider BuildProvider(KunaiOptions? options = null) =>
        new ServiceCollection()
            .AddKunai(options ?? new KunaiOptions(ProfilesDirectory: _profilesDirectory))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    public static IEnumerable<object[]> AbstractionsToResolve() => new[]
    {
        new object[] { typeof(IWiimoteService) },
        new object[] { typeof(ILayerManager) },
        new object[] { typeof(IProfileRepository) },
        new object[] { typeof(IInputOrchestrator) },
        new object[] { typeof(IActionExecutor) },
        new object[] { typeof(IMacroExecutor) },
        new object[] { typeof(IDelayProvider) },
        new object[] { typeof(IProcessLauncher) },
        new object[] { typeof(ISyntheticKeyboard) },
        new object[] { typeof(ISystemActions) },
        new object[] { typeof(ILogService) },
        new object[] { typeof(IUiDispatcher) },
        new object[] { typeof(MainViewModel) }
    };

    [Theory]
    [MemberData(nameof(AbstractionsToResolve))]
    public async Task EveryAbstraction_CanBeResolved(Type type)
    {
        await using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(type));
    }

    [Theory]
    [MemberData(nameof(AbstractionsToResolve))]
    public async Task EveryRegistration_IsASingleton(Type type)
    {
        await using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService(type), provider.GetRequiredService(type));
    }

    [Fact]
    public async Task SharedServices_AreTheSameInstanceAcrossConsumers()
    {
        await using var provider = BuildProvider();

        // ViewModel・Orchestrator・テストが同じ LayerManager / ログを見ていること。
        var viewModel = provider.GetRequiredService<MainViewModel>();
        Assert.Same(provider.GetRequiredService<ILogService>().Entries, viewModel.Log);
        Assert.IsType<LayerManager>(provider.GetRequiredService<ILayerManager>());
        Assert.IsType<InputOrchestrator>(provider.GetRequiredService<IInputOrchestrator>());
        Assert.IsType<LogService>(provider.GetRequiredService<ILogService>());
    }

    [Fact]
    public async Task PlatformImplementations_MatchTheRunningOs()
    {
        await using var provider = BuildProvider();

        if (OperatingSystem.IsLinux())
        {
            Assert.IsType<LinuxSyntheticKeyboard>(provider.GetRequiredService<ISyntheticKeyboard>());
            Assert.IsType<LinuxSystemActions>(provider.GetRequiredService<ISystemActions>());
        }
        else if (OperatingSystem.IsWindows())
        {
            Assert.IsType<WindowsSyntheticKeyboard>(provider.GetRequiredService<ISyntheticKeyboard>());
            Assert.IsType<WindowsSystemActions>(provider.GetRequiredService<ISystemActions>());
        }
    }

    [Fact]
    public void WindowsRegistration_UsesWindowsImplementations()
    {
        var services = new ServiceCollection().AddKunaiWindowsPlatform();

        Assert.Equal(typeof(WindowsSyntheticKeyboard), services.Single(d => d.ServiceType == typeof(ISyntheticKeyboard)).ImplementationType);
        Assert.Equal(typeof(WindowsSystemActions), services.Single(d => d.ServiceType == typeof(ISystemActions)).ImplementationType);
    }

    [Fact]
    public void LinuxRegistration_UsesLinuxImplementations()
    {
        var services = new ServiceCollection().AddKunaiLinuxPlatform();

        Assert.Equal(typeof(LinuxSyntheticKeyboard), services.Single(d => d.ServiceType == typeof(ISyntheticKeyboard)).ImplementationType);
        Assert.Equal(typeof(LinuxSystemActions), services.Single(d => d.ServiceType == typeof(ISystemActions)).ImplementationType);
    }

    [Fact]
    public void CoreRegistration_DoesNotIncludeAnyPlatformImplementation()
    {
        var services = new ServiceCollection().AddKunaiCore(new KunaiOptions());

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISyntheticKeyboard));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(ISystemActions));
    }

    [Fact]
    public async Task ProfilesDirectoryOption_IsUsedByTheRepository()
    {
        await using var provider = BuildProvider(new KunaiOptions(ProfilesDirectory: _profilesDirectory, BundledProfilesDirectory: "/bundled"));

        var options = provider.GetRequiredService<ProfileRepositoryOptions>();

        Assert.Equal(_profilesDirectory, options.ProfilesDirectory);
        Assert.Equal("/bundled", options.DefaultsDirectory);
    }

    [Fact]
    public async Task DefaultOptions_UseUserConfigDirectory_AndBundledProfilesNextToTheExecutable()
    {
        await using var provider = BuildProvider(new KunaiOptions());

        var options = provider.GetRequiredService<ProfileRepositoryOptions>();

        var expectedRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        Assert.Equal(Path.Combine(expectedRoot, "KUNAI", "profiles"), options.ProfilesDirectory);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "profiles"), options.DefaultsDirectory);
    }

    [Fact]
    public async Task BundledProfiles_AreCopiedToTheOutputDirectory()
    {
        await using var provider = BuildProvider(new KunaiOptions());
        var bundled = provider.GetRequiredService<ProfileRepositoryOptions>().DefaultsDirectory!;

        foreach (var name in new[] { "default", "coding", "writing", "browser" })
        {
            Assert.True(File.Exists(Path.Combine(bundled, name + ".json")), $"{name}.json is not next to the executable");
        }
    }

    [Fact]
    public async Task EndToEnd_ViewModelInitializesFromTheBundledProfiles()
    {
        await using var provider = BuildProvider(new KunaiOptions(ProfilesDirectory: _profilesDirectory));
        var viewModel = provider.GetRequiredService<MainViewModel>();

        await viewModel.InitializeAsync();

        Assert.Equal(new[] { "browser.json", "coding.json", "default.json", "writing.json" }, viewModel.ProfileNames);
        Assert.Equal("default.json", viewModel.SelectedProfileName);
        Assert.Equal(new[] { "Enter", "Ctrl+C", "Ctrl+V" }, viewModel.Mappings.Select(m => m.ActionText));

        await viewModel.ShutdownAsync();
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddKunai(null!));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddKunaiCore(null!));
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddKunaiPlatform(null!));
    }
}
