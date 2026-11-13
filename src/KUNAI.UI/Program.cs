using Avalonia;
using KUNAI.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace KUNAI.UI;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // 多重起動の防止。2 つ目の起動は、動いている方のウィンドウを前面に出して終了する。
        // (開発・検証用に --allow-multiple で無効にできる。)
        SingleInstanceGuard? guard = null;
        if (!args.Contains("--allow-multiple"))
        {
            guard = SingleInstanceGuard.CreateDefault();
            if (!guard.TryAcquire())
            {
                guard.SignalExistingInstance();
                guard.Dispose();
                return 0;
            }
        }

        // 依存関係の登録(OS ごとのプラットフォーム実装の切替を含む)。起動時に解決不能な登録があればここで失敗する。
        var services = new ServiceCollection()
            .AddKunai(new KunaiOptions(HideToTray: !args.Contains("--no-tray")));
        if (guard is not null)
        {
            services.AddSingleton(guard);
        }

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        try
        {
            guard?.StartListening();
            return BuildAvaloniaApp(provider).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // WiimoteService は IAsyncDisposable のため、非同期で破棄する。
            // UI は既に終了しており同期コンテキストを持たないので、ここで待ってもデッドロックしない。
            provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
            guard?.Dispose();
        }
    }

    /// <summary>Avalonia の構成。デザイナー/ヘッドレステストからも使えるよう公開している。</summary>
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
