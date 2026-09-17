using ZoneChampion.Core.Input;

namespace ZoneChampion.Core.Tests;

public class ChordTapTrackerTests
{
    private const int LCtrl = KeyChord.VK_LCONTROL;
    private const int RCtrl = KeyChord.VK_RCONTROL;
    private const int LWin = KeyChord.VK_LWIN;
    private const int LShift = KeyChord.VK_LSHIFT;
    private const int Left = 0x25;
    private const int C = 0x43;

    private static readonly KeyStroke[] Mask =
    [
        new(ChordTapTracker.MaskKey, false, true),
        new(ChordTapTracker.MaskKey, false, false),
    ];

    private static KeyStroke Down(int vk) => new(vk, false, true);

    private static KeyStroke Up(int vk) => new(vk, false, false);

    /// <summary>Feeds keys in order and returns how many times the chord toggled.</summary>
    private static int CountToggles(ChordTapTracker tracker, params KeyStroke[] keys) =>
        keys.Count(k => tracker.OnKey(k).Toggle);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TapTogglesOnceAfterEveryKeyIsReleased(bool releaseWinFirst)
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.False(tracker.OnKey(Down(LCtrl)).Toggle);
        Assert.False(tracker.OnKey(Down(LWin)).Toggle);
        var firstUp = tracker.OnKey(Up(releaseWinFirst ? LWin : LCtrl));
        var lastUp = tracker.OnKey(Up(releaseWinFirst ? LCtrl : LWin));

        Assert.False(firstUp.Toggle);
        Assert.True(lastUp.Toggle);
    }

    [Fact]
    public void PressOrderDoesNotMatter()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.Equal(1, CountToggles(tracker, Down(LWin), Down(LCtrl), Up(LCtrl), Up(LWin)));
    }

    [Fact]
    public void AutoRepeatWhileHoldingStillCountsAsOneTap()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.Equal(1, CountToggles(tracker, Down(LCtrl), Down(LWin), Down(LWin), Down(LCtrl), Down(LWin), Up(LWin), Up(LCtrl)));
    }

    [Fact]
    public void CompletingTheChordWithWinSendsTheMaskKeyOnce()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.Empty(tracker.OnKey(Down(LCtrl)).Inject);
        Assert.Equal(Mask, tracker.OnKey(Down(LWin)).Inject);
        Assert.Empty(tracker.OnKey(Down(LWin)).Inject); // repeat
    }

    [Fact]
    public void ChordWithoutWinOrAltNeedsNoMask()
    {
        var tracker = new ChordTapTracker(KeyChord.Parse("Ctrl+Shift"));

        tracker.OnKey(Down(LCtrl));
        Assert.Empty(tracker.OnKey(Down(LShift)).Inject);
    }

    [Fact]
    public void ShortcutStartingWithTheChordDoesNotToggle()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        // Ctrl+Win+Left (switch virtual desktop), then a clean tap still works.
        Assert.Equal(0, CountToggles(tracker, Down(LCtrl), Down(LWin), Down(Left), Up(Left), Up(LWin), Up(LCtrl)));
        Assert.Equal(1, CountToggles(tracker, Down(LCtrl), Down(LWin), Up(LWin), Up(LCtrl)));
    }

    [Fact]
    public void KeyPressedWhileOnlyPartOfTheChordIsHeld_Spoils()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        // Ctrl+C, then Win pressed before Ctrl is released.
        Assert.Equal(0, CountToggles(tracker, Down(LCtrl), Down(C), Up(C), Down(LWin), Up(LWin), Up(LCtrl)));
    }

    [Fact]
    public void KeyPressedAfterPartialReleaseSpoils()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        // Release Win, keep Ctrl, press C (Ctrl+C): not a tap.
        Assert.Equal(0, CountToggles(tracker, Down(LCtrl), Down(LWin), Up(LWin), Down(C), Up(C), Up(LCtrl)));
    }

    [Fact]
    public void IncompleteChordDoesNotToggle()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.Equal(0, CountToggles(tracker, Down(LCtrl), Up(LCtrl), Down(LWin), Up(LWin)));
    }

    [Fact]
    public void BothCtrlKeysMustBeReleased()
    {
        var tracker = new ChordTapTracker(KeyChord.Default);

        Assert.Equal(1, CountToggles(tracker, Down(LCtrl), Down(RCtrl), Down(LWin), Up(LWin), Up(LCtrl), Up(RCtrl)));
    }

    [Fact]
    public void KeyWhoseReleaseWasMissedIsForgotten()
    {
        // RCtrl went down, but its release happened on the lock screen and never reached the hook.
        var physicallyDown = new HashSet<int>();
        var tracker = new ChordTapTracker(KeyChord.Default, physicallyDown.Contains);
        tracker.OnKey(Down(RCtrl));

        physicallyDown.UnionWith([LCtrl, LWin]);
        tracker.OnKey(Down(LCtrl));
        tracker.OnKey(Down(LWin));
        physicallyDown.Remove(LWin);
        Assert.False(tracker.OnKey(Up(LWin)).Toggle);
        physicallyDown.Remove(LCtrl);
        Assert.True(tracker.OnKey(Up(LCtrl)).Toggle);
    }
}
