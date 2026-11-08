using System;
using XSOverlayKeyboardIme.Ime;
using Xunit;

namespace XSOverlayKeyboardIme.Tests;

public sealed class InputModeSwitchTests
{
    [Fact]
    public void ShellClickDisablesOnlyUntilTheCallReturnsWithoutWaitingForANewPicture()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);

        button.Click();
        Assert.False(shown.Enabled);
        button.Click();
        Assert.Equal(1, platform.Shell.Clicks);

        platform.Shell.Complete();
        Assert.True(shown.Enabled);
        Assert.Equal("中", shown.Glyph);
        button.Click();
        Assert.Equal(2, platform.Shell.Clicks);
    }

    [Fact]
    public void LosingHostFocusRevokesTheSwitchWithoutReacquiringForeground()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);

        platform.OwnsFocus = false;
        button.Refresh(platform.ActiveProfile);
        button.Click();

        Assert.Null(shown);
        Assert.Equal(0, platform.Shell.Clicks);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PublicItemPresenceOrQueryFailureNeverFallsThroughToShell(bool publicPresent)
    {
        var platform = new ModePlatform { PublicAbsent = false };
        if (publicPresent)
            platform.Public = new ModeSource { View = new InputModeView(tooltip: "Original mode item") };
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        button.Click();

        Assert.Equal(0, platform.ShellOpens);
        if (publicPresent)
        {
            Assert.Equal("placeholder", shown.Kind);
            Assert.Equal(1, platform.Public.Clicks);
        }
        else
            Assert.Null(shown);
    }

    [Fact]
    public void OldProfileCompletionAndImagesCannotEnableOrReplaceTheNewPendingSwitch()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        button.Click();
        ModeSource old = platform.Shell;
        Action oldChanged = platform.Changed;

        platform.ActiveProfile = new LanguageProfile(
            KeyboardInputType.KeyboardLayout,
            0x409,
            Guid.Empty,
            Guid.Empty,
            (IntPtr)0x409
        );
        platform.Shell = new ModeSource { View = new InputModeView(glyph: "あ") };
        button.Refresh(platform.ActiveProfile);
        button.Click();
        old.Complete();
        oldChanged();

        Assert.False(shown.Enabled);
        Assert.Equal("あ", shown.Glyph);
        platform.Shell.Complete();
        Assert.True(shown.Enabled);
    }

    [Fact]
    public void SystemImagesContinueUpdatingWhileAnInvokeIsPending()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        button.Click();
        platform.Shell.View = new InputModeView(glyph: "英");
        platform.Changed();

        Assert.Equal("英", shown.Glyph);
        Assert.False(shown.Enabled);
        platform.Shell.Complete();
        Assert.True(shown.Enabled);
    }

    [Fact]
    public void FailedCallRestoresClickabilityButMissingTargetRemainsDisabled()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        platform.Shell.FailClick = true;
        Assert.Throws<InvalidOperationException>(button.Click);
        Assert.True(shown.Enabled);

        platform.Shell.View = new InputModeView(glyph: "中", enabled: false);
        platform.Changed();
        button.Click();
        Assert.False(shown.Enabled);
        Assert.Equal(1, platform.Shell.Clicks);
    }

    [Fact]
    public void DuplicateCompletionCannotFinishANewerClickInTheSameProfile()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        using var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        button.Click();
        Action oldCompletion = platform.Shell.Completion;
        oldCompletion();
        button.Click();
        oldCompletion();

        Assert.False(shown.Enabled);
        platform.Shell.Complete();
        Assert.True(shown.Enabled);
    }

    [Fact]
    public void DisposalCannotReopenNativeItemsFromLateRefreshOrCompletion()
    {
        var platform = new ModePlatform();
        InputModeView shown = null;
        var button = new InputModeSwitch(platform, view => shown = view);
        button.Refresh(platform.ActiveProfile);
        button.Click();
        Action changed = platform.Changed;
        button.Dispose();
        button.Refresh(platform.ActiveProfile);
        platform.Shell.Complete();
        changed();
        button.Click();

        Assert.Null(shown);
        Assert.Equal(1, platform.ShellOpens);
        Assert.Equal(1, platform.Shell.Clicks);
    }

    private sealed class ModePlatform : IInputModePlatform
    {
        public LanguageProfile? ActiveProfile { get; set; } =
            new LanguageProfile(KeyboardInputType.TextService, 0x804, Guid.NewGuid(), Guid.NewGuid(), IntPtr.Zero);
        public bool OwnsFocus { get; set; } = true;
        internal ModeSource Shell { get; set; } = new ModeSource();
        internal ModeSource Public { get; set; }
        internal bool PublicAbsent { get; set; } = true;
        internal int ShellOpens { get; private set; }
        internal Action Changed { get; private set; }

        public IInputModeSource OpenPublic(out bool absent)
        {
            absent = PublicAbsent;
            return Public;
        }

        public IInputModeSource OpenShell(Action changed)
        {
            ShellOpens++;
            Changed = changed;
            return Shell;
        }
    }

    private sealed class ModeSource : IInputModeSource
    {
        private Action _complete;
        internal Action Completion => _complete;
        internal int Clicks { get; private set; }
        internal InputModeView View { get; set; } = new InputModeView(glyph: "中");
        internal bool FailClick { get; set; }

        public InputModeView Read() => View;

        public void Click(Action complete)
        {
            Clicks++;
            if (FailClick)
                throw new InvalidOperationException();
            _complete = complete;
        }

        internal void Complete() => _complete();

        public void Dispose() { }
    }
}
