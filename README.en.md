# XSOverlay Keyboard IME

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | **English**

> [!NOTE]
> This document was translated by an LLM. It may contain mistranslations, or tones that differ from the original.

> [!WARNING]
> This repository is still under development. There is no usable build yet, and the implementation is still changing. Please wait.

This mod adds a system IME to the VR keyboard of XSOverlay. Compose text in the input box on the keyboard page. The composing string and the candidates show on the keyboard. Pick a candidate with the keyboard keys. The committed text goes back into the input box.

## Why this Mod

For Chinese input in VRChat, the two solutions I tried did not fit.

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) supports few IMEs and has an awkward UI. It is barely usable.
- [Tapi](https://booth.pm/zh-cn/items/7047187) bundles its own Rime-based input method, so it does not integrate with the system IME. The dictionary you have built up does not carry over.

[OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/) also bundles a virtual keyboard, but it is known to have performance problems, so I did not consider it. I have not tested whether it supports Chinese input, or whether the community has a working setup for it.

Then I found that the XSOverlay keyboard recently added official VRC ChatBox support. So I had an idea: connect it to the system IME, and the dictionary you have built up is ready to use. This mod started there.

## Showcase

TBD

## Requirements

- Windows 10 or later, 64-bit.
- XSOverlay Beta 695. Other versions are untested.
- BepInEx 5.4.23.6, x64. Other versions are untested.
- Only IMEs that implement TSF are supported. This repository is verified with Weasel (Rime for Windows).
- The IME may need inline preedit on. Otherwise you see only the candidates, not the code you are typing. In testing, Weasel required it. In Weasel, set `style/inline_preedit: true` in `weasel.custom.yaml`.

## Installation

TBD

## Roadmap

The next step improves the VRC ChatBox experience. The current version only shows the candidate bar on the keyboard page.

- When the ChatBox button is on, the composing string and the candidates go to the ChatBox in real time, and stay in sync while you type.
- When the input box gains focus, the plugin reports the typing state to VRChat. When focus leaves, it cancels the state.

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

Install the plugin and the native library into the game:

```powershell
dotnet build -c Release -p:DeployToGame=true
```

The repository sets the game path to `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`. If your game is elsewhere, set the path with the `GameDir` property:

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## Credits

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32): TSF input context and composition callbacks. This dependency is compiled into `IngameIME.dll` together with this repository's C shim. The whole library ships under LGPL 2.1; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- DeepSeek V4.1 Flash & DeepSeek Harness: this project was built end to end by 大肥鱼妈妈 (Big Fat Fish Mom).

## Sponsor

**[Sponsor me](https://lgck.cc/sponsor)**

Thank you all for your support! Your sponsorship keeps me creating.

## Contact

- QQ: 3076823485
- QQ group: [168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram: [@lgc2333](https://t.me/lgc2333)
- Email: <lgc2333@126.com>
