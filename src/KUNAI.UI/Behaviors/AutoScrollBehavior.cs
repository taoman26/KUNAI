using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace KUNAI.UI.Behaviors;

/// <summary>ListBox の項目が追加されたら最新の項目までスクロールする添付プロパティ(ログ表示用)。</summary>
public static class AutoScrollBehavior
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<ListBox, bool>("IsEnabled", typeof(AutoScrollBehavior));

    private static readonly ConditionalWeakTable<ListBox, Subscription> Subscriptions = new();

    static AutoScrollBehavior()
    {
        IsEnabledProperty.Changed.AddClassHandler<ListBox>((listBox, _) => Refresh(listBox));
        ItemsControl.ItemsSourceProperty.Changed.AddClassHandler<ListBox>((listBox, _) => Refresh(listBox));
    }

    public static bool GetIsEnabled(ListBox element) => element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ListBox element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>有効/無効や ItemsSource の変化に合わせて、購読を一度解除してから必要なら張り直す。</summary>
    private static void Refresh(ListBox listBox)
    {
        if (Subscriptions.TryGetValue(listBox, out var existing))
        {
            existing.Dispose();
            Subscriptions.Remove(listBox);
        }

        if (GetIsEnabled(listBox) && listBox.ItemsSource is INotifyCollectionChanged collection)
        {
            Subscriptions.Add(listBox, new Subscription(listBox, collection));
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly ListBox _listBox;
        private readonly INotifyCollectionChanged _collection;

        public Subscription(ListBox listBox, INotifyCollectionChanged collection)
        {
            _listBox = listBox;
            _collection = collection;
            _collection.CollectionChanged += OnCollectionChanged;
        }

        public void Dispose() => _collection.CollectionChanged -= OnCollectionChanged;

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
            {
                return;
            }

            // レイアウト確定後にスクロールするため、一度 UI スレッドのキューに積む。
            Dispatcher.UIThread.Post(() =>
            {
                var count = _listBox.ItemCount;
                if (count > 0)
                {
                    _listBox.ScrollIntoView(count - 1);
                }
            }, DispatcherPriority.Background);
        }
    }
}
