using KUNAI.Core.Abstractions;

namespace KUNAI.Actions.Macros;

/// <summary>
/// マクロを先頭から順に実行する。"Delay(ms)" は待機、それ以外は <see cref="IActionExecutor"/> に委譲する。
/// 実行前に全ステップを解析し、不正なステップがあれば 1 つも実行せずに <see cref="FormatException"/> を投げる。
/// アクションが失敗した場合は、その時点で中断して例外を伝播する。
/// </summary>
public sealed class MacroExecutor : IMacroExecutor
{
    private readonly IActionExecutor _actionExecutor;
    private readonly IDelayProvider _delayProvider;

    public MacroExecutor(IActionExecutor actionExecutor, IDelayProvider delayProvider)
    {
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _delayProvider = delayProvider ?? throw new ArgumentNullException(nameof(delayProvider));
    }

    public async Task ExecuteAsync(IReadOnlyList<string> steps, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var parsed = steps.Select(MacroStep.Parse).ToList();

        foreach (var step in parsed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (step.Kind == MacroStepKind.Delay)
            {
                await _delayProvider.DelayAsync(TimeSpan.FromMilliseconds(step.DelayMs), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _actionExecutor.ExecuteAsync(step.Action).ConfigureAwait(false);
            }
        }
    }
}
