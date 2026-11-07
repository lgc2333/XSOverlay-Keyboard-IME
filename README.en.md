# XSOverlay Keyboard IME

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | **English**

> [!NOTE]
> This document was translated from the Chinese version by an LLM. It may contain mistranslations, or tones that differ from the original.

This plugin connects XSOverlay's VR keyboard to the system IME. Compose text in the input box on the keyboard page. The composing string and the candidates show on the keyboard. Pick a candidate with the keyboard keys. The committed text goes back into the input box.

## Why this plugin

For Chinese input in VRChat, the two solutions I tried did not fit.

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) supports few IMEs and has an awkward UI. It is barely usable.
- [Tapi](https://booth.pm/zh-cn/items/7047187) bundles its own Rime-based input method, so it does not integrate with the system IME. The dictionary you have built up does not carry over.

[OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/) also bundles a virtual keyboard, but it is known to have performance problems, so I did not consider it. I have not tested whether it supports Chinese input, or whether the community has a working setup for it.

Then I found that the XSOverlay keyboard recently added official VRC ChatBox support. So I had an idea: connect it to the system IME, and the dictionary you have built up is ready to use. This plugin started there.

## Showcase

TBD

## Requirements

### System and software versions

- Windows 10 or later, 64-bit.
- XSOverlay Beta 695. Other versions are untested.
- BepInEx 5.4.23.6, x64. Other versions are untested.

### IME

- Only IMEs that implement TSF are supported. This repository is verified with Weasel (Rime for Windows).
- The IME may need inline preedit on. Otherwise you see only the candidates, not the code you are typing.
  - In testing, Weasel required it. In Weasel, set `style/inline_preedit: true` in `weasel.custom.yaml`.

## Installation

TBD

## Design trade-offs

<!-- ordered from the most surprising behaviour to the least -->

The behaviours in this section look like faults. They are deliberate.

### Why the candidates cannot be clicked

The candidate bar is display only. Clicking a candidate does nothing. The bar has no paging buttons and no page numbers.

#### Why it is designed this way

This trade-off is deliberate, to keep the code as clean as possible. The plugin aims to work with any IME through public APIs only, and does not add per-IME special cases for now. The cost of that choice shows up here.

Clicking a candidate would make the plugin press keys for you. The same keys do different things in different IMEs: they may move the candidate, page, or do nothing at all. So the keys stay in your hands, and the IME interprets them as it always does.

Page numbers are the same. The numbers an IME reports are unreliable, so the bar shows none rather than a wrong one.

#### What to do instead

Pick candidates and page with the keyboard. A physical keyboard and the keys on the keyboard page both work. Press them the way you already use your IME on a physical keyboard. The plugin only hands the keys to the IME, and the IME decides what they mean. Your IME's own key bindings apply. A key the keyboard page does not have cannot be pressed, so pick a key that is there.

For example, the default keys of rime-frost, the scheme I use:

- Number keys, Space: pick a candidate; `-`, `=`: page
- `↑`, `↓`: move between candidates; `←`, `→`: move the caret in the composing string; `[`, `]`: pick by word; and so on.

### Key results follow the IME and the system, as on a physical keyboard

The plugin forwards ordinary keys to the system and the IME. They decide the result.

When you switch IMEs during composition, the IME decides whether to discard or commit the raw code. The plugin sends no extra keys. Notifications or later keys refresh the name and icon, so updates can wait until your next key.

Shift, Ctrl, and Alt presses and releases reach Windows immediately. Sticky, held, and release rules follow XSOverlay. Closing the keyboard releases these keys and preserves Caps Lock.

The keyboard page uses Win only for Win+Space. Pressing Win alone does not open Start. Other Win combinations do nothing, which prevents accidental desktop windows while typing in VR.

The cost is that the IME's own faults land on the keyboard unchanged. Take Weasel: pressing the 中/英 switch during a composition (the same as clicking the language bar icon, not pressing Shift) leaves the composition open but drops the candidates; switching back to Chinese lets you compose again. Microsoft Pinyin, on the other hand, commits the raw code as it is. This is confusing, but it is the IME's own business, and the plugin does not clean up after it.

### Why the IME name and the 中/英 switch are hidden while the input box has no focus

Once the input box loses the focus, the IME information has no stable source. The plugin cannot get a value that stays true, so whatever it showed could stop matching reality at any moment. Hiding it is better than showing an unreliable name.

The name identifies the IME that receives your next key. It appears while the input box has focus. The 中/英 switch also requires a displayable IME icon. Without an icon, the switch stays hidden.

The bar shows the name and dropdown on the right. The switch covers the left end of Space without changing the row width. When hidden, this area remains part of Space.

The dropdown lists enabled IMEs and ordinary keyboard layouts across all languages. Select an entry to switch, or use Win+Space. Windows shared input settings can also change the IME in desktop windows. An unreadable current IME shows a dimmed “未知输入法” label, with no old icon or selection mark. You can still open the dropdown when its list is available.

### Why it only works in the input box on the keyboard page

Composing means taking the foreground and pressing keys for you. Widen the scope and it reaches into other windows, which costs more than it gains. So the plugin takes the keys of the keyboard page's own input box only. Other input boxes in XSOverlay, the desktop and other applications are out of scope, and will stay out of scope.

### Why composing briefly takes the foreground

There is only one way to deliver keys to the IME: inject global key input. Injected keys reach the foreground window only.

Before sending keys, the plugin tries to bring its hidden window to the foreground. It restores the foreground afterward. If the switch fails, the plugin logs the failure and stops that key. It does not retry.

### Why the bar stays when it is empty

The bar does not go away when it shows no candidates. The keyboard window keeps its height, so the keyboard does not jump while you type.

A composition starts and ends as you type. A bar that came and went with the content would change the height again and again, which is unpleasant in VR.

### Why the send button is grey while composing

The composing string sits in the input box for a moment. The keyboard page enables send whenever the box is not empty, so the pinyin can go out without warning. The plugin therefore keeps send disabled while composing. It returns to normal once the text is committed or cancelled.

## Build

You need .NET SDK 10 or later, CMake, and the C++ build tools of Visual Studio.

Fetch the upstream sources, then build the native library:

```powershell
git submodule update --init
pwsh native/build.ps1
```

Then build the plugin:

```powershell
dotnet build -c Release
```

Install the plugin and the native library into XSOverlay:

```powershell
dotnet build -c Release -p:DeployToGame=true
```

The repository sets the XSOverlay path to `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`. If XSOverlay is elsewhere, set the path with the `GameDir` property:

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## Credits

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32): TSF input context and composition callbacks. This dependency is compiled into `IngameIME.dll` together with this repository's C shim. The whole library ships under LGPL 2.1; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- DeepSeek V4.1 Flash & DeepSeek Harness, GPT 6.1 Sol & Codex: The Fat Whale Mom and the Little Dragon Girl built the entire project.

## Sponsor

**[Sponsor me](https://lgck.cc/sponsor)**

Thank you all for your support! Your sponsorship keeps me creating.

## Contact

- QQ: 3076823485
- QQ group: [168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram: [@lgc2333](https://t.me/lgc2333)
- Email: <lgc2333@126.com>
