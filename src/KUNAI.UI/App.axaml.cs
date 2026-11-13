using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KUNAI.Core.Abstractions;
using KUNAI.UI.DependencyInjection;
using KUNAI.UI.Tray;
using KUNAI.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace KUNAI.UI;

/// <summary>アプリケーションの起動・終了のつなぎ込みのみ。画面のロジックは ViewModel にある。</summary>
public partial class App : Application
{
    private readonly IServiceProvider _services;
    private TrayController? _tray;

    public App(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = _services.GetRequiredService<MainViewModel>();

            var window = (Avalonia.Controls.Window)AvaloniaXamlLoader.Load(
                new Uri("avares://KUNAI.UI/Views/MainWindow.axaml"));
            window.DataContext = viewModel;
            desktop.MainWindow = window;

            // トレイに常駐し、閉じる/最小化では非表示にする。終了はトレイメニューの Exit。
            if (_services.GetRequiredService<KunaiOptions>().HideToTray)
            {
                var log = _services.GetRequiredService<ILogService>();
                var backend = TrayBackendFactory.Create(this, viewModel, log);
                if (backend is not null)
                {
                    _tray = new TrayController(desktop, window, viewModel, backend);
                    log.Add($"System tray: {backend.Name}");
                }
            }

            // 2 つ目の起動を求められたら、このウィンドウを前面に出す(トレイに隠れていても表示する)。
            if (_services.GetService<SingleInstanceGuard>() is { } guard)
            {
                guard.ActivationRequested += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    window.Show();
                    window.WindowState = Avalonia.Controls.WindowState.Normal;
                    window.Activate();
                });
            }

            // 起動時に自動ロード。
            window.Opened += async (_, _) => await viewModel.InitializeAsync();

            // 終了時に自動保存してから、改めて終了する。
            var shutdownDone = false;
            desktop.ShutdownRequested += async (_, e) =>
            {
                if (shutdownDone)
                {
                    return;
                }

                e.Cancel = true;
                await viewModel.ShutdownAsync();
                shutdownDone = true;
                desktop.Shutdown();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
