# JIS 专用键

规则见 [0048](../adr/0048-route-jis-ime-keys-through-the-host.md)。这里记实现、安装版证据与验收边界。安装版三颗按键的键码错误属于 XSOverlay，见 [upstream-xs-overlay](upstream-xs-overlay.md)。

## 实现

- 扩展 [VirtualKey](../../XSOverlayKeyboardIme/Input/VirtualKey.cs) 与 [KeyStrokeReader](../../XSOverlayKeyboardIme/Takeover/KeyStrokeReader.cs)：有效专用原生码在单字符判断之前生成一个原码 stroke。
- 以数值身份识别 `KANA=0x15`、`CONVERT=0x1C`、`NONCONVERT=0x1D`，兼容同值枚举别名；不按日文标签推断键码或行为。
- 保持 Standard 样式、Shift、Ctrl/Alt、Win+Space 与其他 Win 组合的现有规则。
- 0048 只修插件漏接管；它不让安装版的实际键码自动变成标准码。

## 证据

- 真实页面模拟三键：平／片假 `70`、変換 `79`、無変換 `235`。三次 `TryAcceptKey=false`，原游戏随后把日文 `webValue` 标签交给 `KeyboardInputFocusManager.SendKey`（返回 true），composer 保持空白，无组字或待处理输入。`SendKey=true` 只表示已调用 WebView 送键，不证明浏览器插入了文字。
- 被动追踪已部署并取得三颗键的真实页面样本：同步游戏调用带线程局部编号，覆盖原事件、插件判定、原模拟器实参与 `SendKey` 参数。编号只属于该线程的同步调用，不能绑定稍后到达的宿主或页面消息。
- 页面另记键、输入与组字事件的固定分类及长度；未知原文不进入诊断记录，捕获阶段的 `defaultPrevented` 也不代表最终取消状态。
- 同款 Mono 的替代原函数检查覆盖编号嵌套恢复、线程隔离、异常传播与其他补丁抑制原函数；独立 Chromium 检查覆盖正常打字、重复安装、未知文本过滤与监听移除。日志输出失败也已验证不改变原函数返回值、异常与调用次数。这些检查不验证实际 JIS 发送或输入法解释。

## 验收

- 空闲与组字两种状态，覆盖标准码的数字与枚举名、空标签、日文标签与误导性单字符，确认只发送一个原码 stroke。
- 标准码接管通过不代表安装版三颗按键已修复；三键是否送达输入法仍取决于 XSOverlay 的键码处理。
