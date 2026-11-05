# XSOverlay Keyboard IME

> [!WARNING]
> 仓库当前仍处于开发阶段，暂无可用产物，且实现方案仍在变动。请耐心等待。

给 XSOverlay 的 VR 键盘加上系统输入法：在键盘页的输入框里组字，组字串与候选显示在键盘上，用键盘上的按键选候选，提交后写回输入框。

## 运行要求

- Windows 10 或更高版本，64 位。
- XSOverlay Beta 695，其他版本未经验证。
- BepInEx 5.4.23.6，x64 版本，其他版本未经验证。
- 仅支持支持 TSF 的输入法。本仓库使用小狼毫（Rime Windows 版）验证。
- 输入法需要开启行内预编辑，否则只能看到候选，看不到正在输入的编码。小狼毫在 `weasel.custom.yaml` 中设置 `style/inline_preedit: true`。

## 构建

仓库配置的游戏位置为 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`。

安装 .NET SDK 10 或更高版本，然后在本仓库根目录执行：

```powershell
dotnet build -c Release
```

如果游戏不在仓库配置的位置，用 `GameDir` 属性指定路径：

```powershell
dotnet build -c Release -p:GameDir="D:\Steam\steamapps\common\XSOverlay_Beta"
```

## 鸣谢

- [Windmill-City/IngameIME_Win32](https://github.com/Windmill-City/IngameIME_Win32)：输入相关组件实现。
- DeepSeek V4.1 Flash & DeepSeek Harness：本项目全程由大肥鱼妈妈构建。

## 赞助

**[赞助我](https://lgck.cc/sponsor)**

感谢大家的赞助！你们的赞助将是我继续创作的动力！

## 联系

- QQ：3076823485
- QQ群：[168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram：[@lgc2333](https://t.me/lgc2333)
- 邮箱：<lgc2333@126.com>
