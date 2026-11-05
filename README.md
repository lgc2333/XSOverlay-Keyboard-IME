# XSOverlay Keyboard IME

给 XSOverlay 的 VR 键盘加上系统输入法：打字时显示候选栏，点选候选直接上屏。

## 运行要求

- Windows 10 或更高版本，64 位。
- XSOverlay Beta 695，其他版本未经验证。
- BepInEx 5.4.23.6，x64 版本，其他版本未经验证。
- 仅支持支持 TSF 的输入法。本仓库使用小狼毫（Rime Windows 版）验证。
- 输入法需要开启行内预编辑，否则只能看到候选，看不到正在输入的编码。小狼毫在 `weasel.custom.yaml` 中设置 `style/inline_preedit: true`。

## 现状

组字管线已实现：插件在游戏进程内托管 TSF 组字上下文，能读候选、组字串与提交文本。键盘页接管与候选栏尚未实现。

自检可以验证这条管线。自检会抢占前台、向隐藏输入框喂键，然后交还前台。

1. 打开 `BepInEx\config\cc.lgck.xsoverlay.keyboardime.cfg`。
2. 把 `SelfCheck/Enabled` 改为 `true`。
3. 启动游戏。
4. 在 `BepInEx\LogOutput.log` 里查看以 `[ime]` 开头的行。

自检成功时，日志包含 `composition started`、`candidates count=` 与 `committed text`。自检失败时，日志说明失败原因。

`SelfCheck/Text` 指定自检输入的编码，默认为 `nihc`。该编码需在当前输入法下能组成候选。

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

## 赞助

**[赞助我](https://lgck.cc/sponsor)**

感谢大家的赞助！你们的赞助将是我继续创作的动力！

## 联系

- QQ：3076823485
- QQ群：[168603371](https://qm.qq.com/q/EikuZ5sP4G)
- Telegram：[@lgc2333](https://t.me/lgc2333)
- 邮箱：<lgc2333@126.com>
