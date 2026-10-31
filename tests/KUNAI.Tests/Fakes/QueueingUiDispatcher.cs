using KUNAI.Core.Abstractions;

namespace KUNAI.Tests.Fakes;

/// <summary>Post された処理を溜めておき、<see cref="RunAll"/> で実行する(UI スレッドの代わり)。</summary>
public sealed class QueueingUiDispatcher : IUiDispatcher
{
    private readonly Queue<Action> _queue = new();

    public int Pending => _queue.Count;

    public void Post(Action action) => _queue.Enqueue(action);

    public void RunAll()
    {
        while (_queue.Count > 0)
        {
            _queue.Dequeue()();
        }
    }
}
