using Avalonia;
using KUNAI.UI.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace KUNAI.UI;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // 依存関係の登録(OS ごとのプラットフォーム実装の切替を含む)。起動時に解決不能な登録があればここで失敗する。
        var provider = new ServiceCollection()
            .AddKunai(new KunaiOptions(HideToTray: !args.Contains("--no-tray")))
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

        try
        {
            return BuildAvaloniaApp(provider).StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // WiimoteService は IAsyncDisposable のため、非同期で破棄する。
            // UI は既に終了しており同期コンテキストを持たないので、ここで待ってもデッドロックしない。
            provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>Avalonia の構成。デザイナー/ヘッドレステストからも使えるよう公開している。</summary>
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services) =>
        AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
