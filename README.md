# XSOverlay Keyboard IME

**简体中文** | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [English](README.en.md)

> [!WARNING]
> 仓库当前仍处于开发阶段，暂无可用产物，且实现方案仍在变动。请耐心等待。

给 XSOverlay 的 VR 键盘加上系统输入法：在键盘页的输入框里组字，组字串与候选显示在键盘上，用键盘上的按键选候选，提交后写回输入框。

## 为什么做这个 Mod

VRChat 里的中文输入，试过的两个方案都不够用。

- [OVR Input Method Bridge](https://store.steampowered.com/app/3448100/OVR_Input_Method_Bridge/) 支持的输入法种类少，界面也不顺手，只算勉强可用。
- [Tapi](https://booth.pm/zh-cn/items/7047187) 自带一套基于 Rime 的输入方案，不和系统输入法打通，按习惯养出来的词库带不过去。

另有 [OVR Toolkit](https://store.steampowered.com/app/1068820/OVR_Toolkit/)，自带虚拟键盘，但已知有性能问题，因此未纳入考虑；它能否输入中文、社区有无现成方案，我同样未实测。

正巧我发现 XSOverlay 的键盘近期官方支持了 VRC ChatBox 功能。所以我有了这个想法：把它接上系统输入法，养好的词库就能直接用。这个 Mod 由此开始。

## Showcase

TBD

## 运行要求

- Windows 10 或更高版本，64 位。
- XSOverlay Beta 695，其他版本未经验证。
- BepInEx 5.4.23.6，x64 版本，其他版本未经验证。
- 仅支持实现了 TSF 的输入法。本仓库使用小狼毫（Rime Windows 版）验证。
- 输入法可能需要开启行内预编辑，否则只能看到候选，看不到正在输入的编码。实测小狼毫必需，其应在 `weasel.custom.yaml` 中设置 `style/inline_preedit: true`。

## 安装

TBD

## 设计取舍

### 候选栏相关：候选项不可点击，不可切换候选，没有额外翻页按键，不显示页码

目的是为了做技术上最干净的实现，这样不需要针对各个输入法做一遍单独的适配。插件不替你合成按键：合成键一旦不被输入法接受，可能悄悄写错上屏文字。

请用你在物理键盘上的习惯操作候选，以及使用输入法，如我的 Rime 配置：数字键/空格选择，左右中括号以词定字，-=键左右翻页，上下方向键切换候选，左右方向键移动预编辑光标等

## 路线图

下一步优化 VRC ChatBox 体验。当前版本只在键盘页上显示候选栏。

- ChatBox 按钮打开后，组字串与候选实时送进 ChatBox，输入过程中同步显示。
- 输入框获得焦点时向 VRChat 上报正在输入，失焦时取消。

## 构建

需要 .NET SDK 10 或更高版本、CMake，以及 Visual Studio 的 C++ 生成工具。

先取上游源码，再编译原生库：

```powershell
git submodule update --init
pwsh native/build.ps1
```

然后编译插件：

```powershell
dotnet build -c Release
```

把插件与原生库一起装进游戏：

```powershell
dotnet build -c Release -p:DeployToGame=true
```

仓库配置的游戏位置为 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`。游戏不在该位置时，用 `GameDir` 属性指定路径：

```powershell
dotnet build -c Release -p:DeployToGame=true -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## 鸣谢

- [Windmill-City/IngameIME-Common](https://github.com/Windmill-City/IngameIME-Common), [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32)：TSF 输入上下文与组字回调。此依赖与本仓库的 C 薄壳一起编进 `IngameIME.dll`，整份按 LGPL 2.1 分发，见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
- DeepSeek V4.1 Flash & DeepSeek Harness：本项目全程由大肥鱼妈妈构建。

## 赞助

**[赞助我](https://lgck.cc/sponsor)**

感谢大家的赞助！你们的赞助将是我继续创作的动力！

## 联系

- QQ：3076823485
- QQ群：[168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram：[@lgc2333](https://t.me/lgc2333)
- 邮箱：<lgc2333@126.com>
