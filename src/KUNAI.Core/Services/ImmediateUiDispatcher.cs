using KUNAI.Core.Abstractions;

namespace KUNAI.Core.Services;

/// <summary>その場で同期実行する <see cref="IUiDispatcher"/>。UI を持たない環境(テスト・コンソール)向け。</summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
