using Avalonia.Threading;
using KUNAI.Core.Abstractions;

namespace KUNAI.UI;

/// <summary>Avalonia の UI スレッドへ処理を渡す <see cref="IUiDispatcher"/>。</summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
