using KUNAI.Core.Domain;

namespace KUNAI.Tests.Core;

public class StandardActionParserTests
{
    [Theory]
    [InlineData("LaunchBrowser", StandardAction.LaunchBrowser)]
    [InlineData("launchterminal", StandardAction.LaunchTerminal)]
    [InlineData("  MUTE ", StandardAction.Mute)]
    [InlineData("BrowserBack", StandardAction.BrowserBack)]
    public void Parses_NamesCaseInsensitively(string text, StandardAction expected)
    {
        Assert.True(StandardActionParser.TryParse(text, out var action));
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Ctrl+C")]
    [InlineData("LaunchBrowser,Mute")]
    public void Rejects_NumbersAndNonNames(string? text)
    {
        Assert.False(StandardActionParser.TryParse(text, out _));
    }
}
