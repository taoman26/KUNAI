using KUNAI.Actions.Executors;
using KUNAI.Actions.Macros;
using KUNAI.Core.Abstractions;
using KUNAI.Core.Services;
using KUNAI.Input.Wiimote;
using KUNAI.Platform.Linux;
using KUNAI.Platform.Linux.Bluetooth;
using KUNAI.Platform.Windows;
using KUNAI.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KUNAI.UI.DependencyInjection;

/// <summary>
/// 依存関係の登録。Core は抽象だけを持ち、実装はここ(Composition Root)で結び付ける。
/// OS 依存の実装(キー送出・システムアクション)は実行中の OS に応じて登録を切り替える。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>アプリ全体(共通部分 + 実行中の OS 向けのプラットフォーム実装)を登録する。</summary>
    public static IServiceCollection AddKunai(this IServiceCollection services, KunaiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services
            .AddKunaiCore(options ?? new KunaiOptions())
            .AddKunaiPlatform();
    }

    /// <summary>OS に依存しない部分を登録する。</summary>
    public static IServiceCollection AddKunaiCore(this IServiceCollection services, KunaiOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        // 設定
        services.AddSingleton(options);
        services.AddSingleton(ResolveRepositoryOptions);
        services.AddSingleton(new SystemActionsOptions());
        services.AddSingleton(WiimoteServiceOptions.Default);

        // Core
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<ILayerManager, LayerManager>();
        services.AddSingleton<IProfileRepository, ProfileRepository>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IInputOrchestrator, InputOrchestrator>();

        // Input
        services.AddSingleton<IWiimoteTransport, HidSharpWiimoteTransport>();
        services.AddSingleton<IWiimoteService, WiimoteService>();

        // Actions
        services.AddSingleton<IDelayProvider, TaskDelayProvider>();
        services.AddSingleton<IActionExecutor, ActionExecutor>();
        services.AddSingleton<IMacroExecutor, MacroExecutor>();

        // UI
        services.AddSingleton<MainViewModel>();

        return services;
    }

    /// <summary>実行中の OS 向けの <see cref="ISyntheticKeyboard"/> と <see cref="ISystemActions"/> を登録する。</summary>
    public static IServiceCollection AddKunaiPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (OperatingSystem.IsWindows())
        {
            return services.AddKunaiWindowsPlatform();
        }

        if (OperatingSystem.IsLinux())
        {
            return services.AddKunaiLinuxPlatform();
        }

        throw new PlatformNotSupportedException("KUNAI supports Windows and Linux only.");
    }

    public static IServiceCollection AddKunaiWindowsPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISyntheticKeyboard, WindowsSyntheticKeyboard>();
        services.AddSingleton<ISystemActions, WindowsSystemActions>();
        services.AddSingleton<IWiimotePairing, WindowsWiimotePairing>();
        return services;
    }

    public static IServiceCollection AddKunaiLinuxPlatform(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // LinuxSyntheticKeyboard は IDisposable。コンテナの破棄時に仮想デバイスが削除される。
        services.AddSingleton<ISyntheticKeyboard, LinuxSyntheticKeyboard>();
        services.AddSingleton<ISystemActions, LinuxSystemActions>();

        // Wii Remote の自動接続: 探索して L2CAP で直接接続し、その接続を優先して使う(無ければ hidraw から探す)。
        services.AddSingleton<LinuxWiimoteLink>();
        services.AddSingleton<IWiimotePairing, LinuxWiimotePairing>();
        services.AddSingleton<IWiimoteTransport>(sp =>
            new LinuxWiimoteTransport(new HidSharpWiimoteTransport(), sp.GetRequiredService<LinuxWiimoteLink>()));
        return services;
    }

    private static ProfileRepositoryOptions ResolveRepositoryOptions(IServiceProvider provider)
    {
        var options = provider.GetRequiredService<KunaiOptions>();
        var bundled = options.BundledProfilesDirectory ?? Path.Combine(AppContext.BaseDirectory, "profiles");

        return options.ProfilesDirectory is { Length: > 0 } directory
            ? new ProfileRepositoryOptions(directory, bundled)
            : ProfileRepositoryOptions.CreateDefault(bundled);
    }
}
