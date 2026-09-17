using ZoneChampion.Core.Input;

namespace ZoneChampion.Core.Tests;

public class KeyChordTests
{
    [Theory]
    [InlineData("Ctrl+Win", "Ctrl+Win")]
    [InlineData(" control + windows ", "Ctrl+Win")]
    [InlineData("lctrl+LWIN", "LCtrl+LWin")]
    [InlineData("Alt+Shift", "Alt+Shift")]
    [InlineData("RCtrl", "RCtrl")]
    public void ParsesModifierNames(string text, string expected)
    {
        Assert.Equal(expected, KeyChord.Parse(text).ToString());
    }

    [Theory]
    [InlineData("Ctrl+Space", "\"Space\" isn't a modifier key")]
    [InlineData("Tab+Win", "\"Tab\" isn't a modifier key")]
    [InlineData("Ctrl+LCtrl", "overlaps")]
    [InlineData("Ctrl++Win", "\"\" isn't a modifier key")]
    [InlineData("", "no keys")]
    public void RejectsInvalidChords(string text, string expectedError)
    {
        Assert.False(KeyChord.TryParse(text, out _, out var error));
        Assert.Contains(expectedError, error);
    }

    [Fact]
    public void EitherSideSatisfiesAGenericKey()
    {
        var chord = KeyChord.Parse("Ctrl+Win");

        Assert.True(chord.IsSatisfiedBy([KeyChord.VK_RCONTROL, KeyChord.VK_LWIN]));
        Assert.False(chord.IsSatisfiedBy([KeyChord.VK_LCONTROL, KeyChord.VK_RCONTROL]));
        Assert.True(chord.Contains(KeyChord.VK_RWIN));
        Assert.False(chord.Contains(KeyChord.VK_LSHIFT));
    }

    [Fact]
    public void SideSpecificKeyOnlyMatchesThatSide()
    {
        var chord = KeyChord.Parse("LCtrl+Win");

        Assert.False(chord.IsSatisfiedBy([KeyChord.VK_RCONTROL, KeyChord.VK_LWIN]));
        Assert.False(chord.Contains(KeyChord.VK_RCONTROL));
    }
}
