namespace KUNAI.Core.Abstractions;

/// <summary>UI スレッドへの処理の受け渡し。バックグラウンドからの通知で UI にバインドされた状態を更新するために使う。</summary>
public interface IUiDispatcher
{
    /// <summary>UI スレッドで実行するよう予約する。待たずに戻る。</summary>
    void Post(Action action);
}
