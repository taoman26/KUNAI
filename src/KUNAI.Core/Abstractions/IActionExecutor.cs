namespace KUNAI.Core.Abstractions;

/// <summary>単一のアクション(標準アクション名またはショートカット)を実行する。</summary>
public interface IActionExecutor
{
    Task ExecuteAsync(string action);
}
