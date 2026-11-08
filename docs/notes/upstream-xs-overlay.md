# XSOverlay 侧缺陷

以下两处来源都在 XSOverlay 自身，插件不修，只记录证据，也不继续调查修复方案。JIS 专用键的插件侧接管见 [jis-ime-keys](jis-ime-keys.md)。

## JIS 残留扫描码被当成虚拟键码

- 安装布局 `Keyboard.json` 的 `jisRows` 保存扫描码字符串，注明 `Keycodes are actually ScanCodes`：無変換 `7B`、変換 `79`、平／片假 `70`。
- `KeyboardHelper.TryResolveKeycode` 按十六进制解析扫描码，再调 `MapVirtualKeyExW(..., 3, hkl)`；转换失败时保留原字符串。当前系统的日语与美式布局把 `0x7B` 映射为 `235`，`0x79` 与 `0x70` 返回零，残留的 `70/79` 随后在 `InputManager.HandleKeypress` 被按十进制解释为虚拟键码 `F`/`O`。
- 页面 `hexMaybe` 只做 `String(code)`，按钮事件值与日文标签各自保留。真实页面事件已确认是 `70/79/235`，不是标准专用虚拟键码 `15/1C/1D`。
- 没有游戏 WebView 焦点时，原模拟器按虚拟键码 `SendInput`；有焦点时优先使用 `webValue` 日文标签。因此桌面路径把平／片假注入为 `F`、変換注入为 `O`，能解释 VSCode 里看到的 `f`；無変換的 `a` 不能由 `235` 或 `79 → O` 推出，完整链路未确认。`235` 是系统实际映射，其 OS 事件与输入法解释未验证，不一起判错。
- 插件能做的只有按标准原生键码接管（[0048](../adr/0048-route-jis-ime-keys-through-the-host.md)），不能让安装版三颗按键自动变成标准码。
- 证据版本：`Keyboard.json` SHA-256 `DF90A2CFFDF45A731B502EA52BBC6C3716C2555F1B71C53329BDB64CB1454282`，`Assembly-CSharp.dll` SHA-256 `88584707BAD341D0247E8AF1277CC86296DF3B2B796972E5CAEF03EF214B6F93`。未修改安装布局或游戏程序集。

## 失焦时 Alt+Shift+F 只打出大写 F

- 用户报告：VR 内 Alt 粘滞与保持时快捷键失效，物理 Alt+Z 有效；另有一次页面状态与实际按下反向，重新聚焦并失焦 composer 后恢复。重启后重复双击未再复现状态反转。
- 最后三次对照（同一 VSCode 窗口与线程，用 VR Alt + Z 复现同一条修饰键链路）：

| 操作                  | Windows 低级事件                                       | VSCode 收到的事件与结果                                            |
| --------------------- | ------------------------------------------------------ | ------------------------------------------------------------------ |
| VR Alt 粘滞后按 Z     | LAlt，扫描码 `0x38`，extended=true；Z 带系统 Alt 标记  | AltRight / AltGraph；Z 的原始与转换后 modifiers 均为空，只解析为 Z |
| VR Alt 双击保持后按 Z | 连续两次同样的 LAlt Down；Z 带系统 Alt 标记            | AltRight / AltGraph；Z 的 modifiers 仍为空，只解析为 Z             |
| 物理 Alt+Z            | LAlt，扫描码 `0x38`，extended=false；Z 带系统 Alt 标记 | AltLeft / Alt；Z 带 alt，执行 `editor.action.toggleWordWrap`       |

- 三次游戏焦点均为 false，修饰键转发均返回 false，走的都是 XSOverlay 原始模拟器。安装版 `WindowsInput.InputBuilder.IsExtendedKey` 把 MENU、LMENU、RMENU 都判为扩展键，`AddKeyDown`/`AddKeyUp` 据此设置 extended 标记。
- Chromium 把 extended 位并入扫描码，键位表把 `0x38` 与 `0xe038` 分别解释为 AltLeft 与 AltRight；AltGraph 分支把支持 AltGraph 布局下带扩展标记的 MENU 解释为 AltGraph。目标程序因此把 VR 的 Alt 当成 AltRight/AltGraph，字母缺少 Alt。
- 这是 XSOverlay 送键路径的缺陷，不能作为插件接管失败来增加补偿按键。本机旧式语言切换热键已禁用，VSCode 用户快捷键文件没有覆盖 Alt+Shift+F；扩展覆盖与编辑器上下文仍未排除，但目标程序记录已直接证明该次失败属于 AltRight/AltGraph。

## 状态

两处都只记录，不修复，不继续调查修复方案。JIS 专用键已移出插件待解决项；無変換 `a` 的来源保留为证据边界。
