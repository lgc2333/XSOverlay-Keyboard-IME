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

## 展示

TBD

## 运行要求

### 系统 / 软件版本

- Windows 10 或更高版本，64 位。
- XSOverlay Beta 695，其他版本未经验证。
- BepInEx 5.4.23.6，x64 版本，其他版本未经验证。

### 输入法

- 仅支持实现了 TSF 的输入法。本仓库使用小狼毫（Rime Windows 版）验证。
- 输入法可能需要开启行内预编辑，否则只能看到候选，看不到正在输入的编码。
  - 实测小狼毫必需，其须在 `weasel.custom.yaml` 中设置 `style/inline_preedit: true`。

## 安装

TBD

## 设计取舍

本节的行为看起来像故障，其实是有意为之。

### 候选为什么点不了

候选栏只能看，点它不会有反应，也没有翻页按钮和页码。

#### 替代方案

选候选、翻页都请按键盘，物理键盘和键盘页上的按键都可以，按你平时在物理键盘上用输入法的习惯操作。插件只负责把按键转交给输入法，按键是什么意思由输入法自己决定。键位以你自己的输入法配置为准；键盘页上没有的键按不了，请挑键盘上有的键用。

以我正在使用的 rime-frost（白霜拼音）方案的默认键位为例：

- 数字键、空格：选候选；`-`、`=`：翻页
- `↑`、`↓`：换候选；`←`、`→`：移动组字串里的光标；`[`、`]`：以词定字；等等……

#### 为什么这么设计

这是刻意的取舍，为了在代码实现上最干净。本插件争取通过公共 API 兼容任意输入法，且暂时不想针对任何输入法做单独适配，代码里没有「某某输入法」的特例。这个取舍的弊端就体现在此。

点选候选要插件替你按键，但同一串键在不同输入法里的行为并不一样：可能换候选，可能翻页，也可能毫无反应。所以按键交回给你，输入法怎么解释就怎么执行。

页码同理，输入法报告的页码不可靠，宁可不显示也不显示错的。

另外候选栏宽度有限，放不下的候选不显示，请尝试翻页。

### 为什么只在键盘页的输入框里生效

组字要抢前台、替你按键，范围一放宽就会管到别的窗口，风险比收益大。所以插件只接管键盘页自带输入框里的按键，游戏里的其他输入框、桌面、别的应用都不接管，以后也不打算支持。

### 为什么组字时发送键是灰的

组字串临时放在输入框里，键盘页见输入框非空就会让发送键可点，不打一声招呼就能把拼音发出去。所以插件在组字期间把它压回禁用。上屏或取消后恢复正常。

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
