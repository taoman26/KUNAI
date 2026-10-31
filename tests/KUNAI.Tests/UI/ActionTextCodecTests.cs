using KUNAI.Core.Domain;
using KUNAI.UI.ViewModels;

namespace KUNAI.Tests.UI;

public class ActionTextCodecTests
{
    [Fact]
    public void Format_SingleAction_IsTheActionText()
    {
        var mapping = new ButtonMapping { Button = WiimoteButton.A, Action = "Ctrl+C" };

        Assert.Equal("Ctrl+C", ActionTextCodec.Format(mapping));
    }

    [Fact]
    public void Format_Macro_JoinsStepsWithSemicolons_AndIgnoresAction()
    {
        var mapping = new ButtonMapping
        {
            Action = "ignored",
            Macro = new List<string> { "Ctrl+S", "Delay(300)", "Alt+Tab" }
        };

        Assert.Equal("Ctrl+S; Delay(300); Alt+Tab", ActionTextCodec.Format(mapping));
    }

    [Fact]
    public void Format_EmptyMacroList_FallsBackToAction()
    {
        var mapping = new ButtonMapping { Action = "Enter", Macro = new List<string>() };

        Assert.Equal("Enter", ActionTextCodec.Format(mapping));
    }

    [Fact]
    public void Apply_SingleAction_ClearsMacro()
    {
        var mapping = new ButtonMapping { Macro = new List<string> { "A" } };

        ActionTextCodec.Apply("  Ctrl+C  ", mapping);

        Assert.Equal("Ctrl+C", mapping.Action);
        Assert.Null(mapping.Macro);
    }

    [Fact]
    public void Apply_TextWithSemicolons_BecomesMacro_AndClearsAction()
    {
        var mapping = new ButtonMapping { Action = "old" };

        ActionTextCodec.Apply("Ctrl+C;Delay(100) ; Ctrl+V", mapping);

        Assert.Equal(string.Empty, mapping.Action);
        Assert.Equal(new[] { "Ctrl+C", "Delay(100)", "Ctrl+V" }, mapping.Macro!);
    }

    [Fact]
    public void Apply_DropsEmptySteps()
    {
        var mapping = new ButtonMapping();

        ActionTextCodec.Apply("A;; ;B;", mapping);

        Assert.Equal(new[] { "A", "B" }, mapping.Macro!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Apply_BlankText_MeansUnassigned(string? text)
    {
        var mapping = new ButtonMapping { Action = "old", Macro = new List<string> { "x" } };

        ActionTextCodec.Apply(text, mapping);

        Assert.Equal(string.Empty, mapping.Action);
        Assert.Null(mapping.Macro);
    }

    [Theory]
    [InlineData("Ctrl+S; Delay(300); Alt+Tab", "Ctrl+S; Delay(300); Alt+Tab")]
    [InlineData("Enter", "Enter")]
    public void FormatAfterApply_RoundTrips(string text, string expected)
    {
        var mapping = new ButtonMapping();

        ActionTextCodec.Apply(text, mapping);

        Assert.Equal(expected, ActionTextCodec.Format(mapping));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+C")]
    [InlineData("ctrl+shift+t")]
    [InlineData("Enter")]
    [InlineData("1")]
    [InlineData("LaunchBrowser")]
    [InlineData("lockscreen")]
    [InlineData("BrowserBack")]
    [InlineData("Ctrl+C; Delay(100); Ctrl+V")]
    [InlineData("Delay(0); Enter")]
    [InlineData("LaunchTerminal; Delay(500); Ctrl+L")]
    [InlineData("Ctrl+C;")]
    public void Validate_AcceptsValidText(string? text)
    {
        Assert.Null(ActionTextCodec.Validate(text));
    }

    [Theory]
    [InlineData("Nonsense", "Nonsense")]
    [InlineData("Ctrl+Nonsense", "Ctrl+Nonsense")]
    [InlineData("LaunchRocket", "LaunchRocket")]
    [InlineData("Ctrl+C; Bogus", "Bogus")]
    [InlineData("Ctrl+", "Ctrl+")]
    public void Validate_RejectsUnknownActions_NamingTheCulprit(string text, string culprit)
    {
        var error = ActionTextCodec.Validate(text);

        Assert.NotNull(error);
        Assert.Contains(culprit, error);
    }

    [Theory]
    [InlineData("Delay(100)")]          // マクロ(セミコロン区切り)の外では Delay は使えない
    [InlineData("Ctrl+C; Delay(abc)")]
    [InlineData("Ctrl+C; Delay(-5); Ctrl+V")]
    [InlineData("Ctrl+C; Delay(60001)")]
    [InlineData("Ctrl+C; Delay()")]
    public void Validate_RejectsBadDelays(string text)
    {
        Assert.NotNull(ActionTextCodec.Validate(text));
    }
}
