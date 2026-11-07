# 三项已定位的 VR 问题（2026-11-07）

本文件供新对话接手三项已定位问题：回车丢焦、中英切换按钮覆盖空格、候选栏不支持左右摇杆。三项均未修复，当前状态见 [路线图](../ROADMAP.md)。本轮只调查和更新文档，用户要求不要 commit。

## 基线与边界

- 先读 [AGENTS.md](../../AGENTS.md) 与 [上一份交接](./26-11-07-3_native-input-and-vr-feedback.md)。源码 HEAD 为 `8b6003d`，工作区已有路线图更新，接手前检查差异。
- 安装版仍为 `ce68c56`；本地 `d3f3f02` 的初始焦点修复尚未部署。最近核对的安装版插件 SHA-256 前缀为 `F1BC91535166ACE4`，本地为 `FCE84EFD71A05591`。
- 本对话继续调查清空未结束组字、输入法切换回退、Win+空格后抢焦及下拉列表异常。新对话先处理本文件的三项，修改共享文件前重新核对工作区。
- 保留输入法原生结果，遵循 [0041](../adr/0041-no-recovery-for-native-ime-results.md)。实现方案涉及新设计决定时，按 AGENTS.md 的 ADR 规则先与用户确认。

下文 `GameDir` 指 XSOverlay 安装目录，默认位置见 AGENTS.md。真实键盘页脚本位于 `GameDir/XSOverlay_Data/StreamingAssets/Plugins/Applications/UserInterface/Default/Keyboard/js/`，下文脚本文件名均相对此目录。调查只读取安装文件，未修改 `StreamingAssets`。

## 回车丢焦

**来源：插件遗漏原页面提前消费回车的入口。**

1. 原页 `keyboard-view-keys.js` 的 `sendKey()` 在 composer 输入框聚焦时特判 `RETURN`，执行 `onComposerSubmit()` 后返回，不调用普通的 `onKeyboardEvent()`。
2. `keyboard.js` 把此回调接到 `KeyboardSpeech.sendComposerText()`。
3. `keyboard-speech-composer.js` 的 `sendComposerText()` 读取输入值，先请求释放焦点，再发送 `SendKeyboardText`，没有检查插件是否正在组字。
4. 插件只在 [KeyboardTakeoverPatch](../../XSOverlayKeyboardIme/Game/Patches/KeyboardTakeoverPatch.cs) 接管 `InputManager.HandleKeypress`。这次回车没有到达该方法，输入法没有收到 Return。
5. 原页释放焦点后，插件的失焦取消路径可能送 Escape；后续提交也可能被 [insertText](../../web/src/composer.ts) 判为 `focus-lost`。

隔离测试使用与安装版一致的真实页面脚本，注入当前插件页面脚本并显示组字，然后按真实 RETURN 控件。捕获到 `ReleaseKeyboardInputFocus`、`SendKeyboardText`、`PlayDeviceHaptics`，输入框随后失焦。运行日志中 `feed Return` 为 0，与此一致。

修复要覆盖 VR RETURN 控件与 `keyboard-speech-view.js` 的 DOM `keydown` 回车入口。组字中让回车进入既有输入法送键路径，空闲时保留原页发送行为；不要把未提交编码当成提交文本写回。发送按钮禁用不能阻止这条快捷路径。

验收：组字中按真实回车，输入法决定提交结果，composer 输入框保留焦点，文本不发送到系统或 VRChat；空闲回车仍按原页行为发送。

## 中英切换按钮覆盖空格

**来源：插件现有布局设计。**

[switch-key.ts](../../web/src/switch-key.ts) 的 `mount()` 把按钮绝对定位到 `space.offsetLeft/offsetTop`，空格本体没有缩短。[0037](../adr/0037-the-page-owns-the-composer-state.md) 明确要求按钮盖在空格左端，因此重叠不是随机排版故障。图标固定显示为 32px，并使用 `image-rendering: pixelated`；具体外观需结合真实主题验收。

位置方案尚未决定。调整位置涉及 0037 的既有决定，应先与用户确认方案。继续保留 [0042](../adr/0042-hide-the-switch-without-an-icon.md) 的显示条件：composer 输入框聚焦且图标可显示时才出现。

验收：各物理布局、布局格式与主题下，按钮不覆盖空格，不挤出右 Ctrl，聚焦与失焦不改变键盘行宽，重建后仅有一个按钮。

## 候选栏左右摇杆滚动

**来源：XSOverlay 滚动通路仅处理纵轴，插件没有补接横轴。**

- `GameDir/XSOverlay_Data/StreamingAssets/SteamVR/actions.json` 把 `Scroll` 定义为 `vector2`，默认 Knuckles 与 Oculus Touch 绑定已将右摇杆接到此动作。
- 对安装版 `Managed/Assembly-CSharp.dll` 的反编译确认：`Raycaster.HandleScrolling()` 只读取 `InputDevice.Scroll.axis.y`，调用 `webView.PointToNormalized(0, -pixelDelta)` 后再 `webView.Scroll(...)`，横轴未传给网页。
- [candidate-bar.css](../../web/src/candidate-bar.css) 只启用横向溢出；[candidate-bar.ts](../../web/src/candidate-bar.ts) 只处理选中项自动揭示与滚动渐隐，没有摇杆或滚轮转换。
- 页面 `.key` 等交互类只控制自定义悬停标记，未发现它们阻止滚动的证据。已有网页光标通知不包含摇杆轴。

仅加网页滚轮处理可把上下滚动映射成横向浏览，无法恢复左右轴。真正支持左右摇杆需要把横轴接到候选区域；接线方案尚未决定。按 [0018](../adr/0018-candidate-bar-display-only.md) 只滚动候选视口，不改变输入法选中项，不合成选词或翻页键。

验收：射线指向候选区域时左右摇杆可浏览溢出内容，离开区域不影响其他浮窗；输入法选中项与组字保持不变。需核对用户手柄的实际绑定与双手射线目标。

## 验证限制

隔离页面确认的是页面事件与焦点行为，不能替代真实输入法与 VR 验收。上述调查没有改源码、停止 XSOverlay 或部署。实现后按 AGENTS.md 完成对应检查，再更新路线图的实际结果；没有跑过的场景保留未验证状态。
