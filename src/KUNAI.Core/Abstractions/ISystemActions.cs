using KUNAI.Core.Domain;

namespace KUNAI.Core.Abstractions;

/// <summary>ブラウザ起動・音量・画面ロックなどの OS 機能。プラットフォーム層が実装する。</summary>
public interface ISystemActions
{
    Task ExecuteAsync(StandardAction action);
}
