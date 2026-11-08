# VRC ChatBox 实时广播调查（2026-11-08）

本次已确认行为约定并完成实现，桌面 OSC 实测与本地自动化验证通过，XSOverlay 完整 VR 验收待完成。

## 已确认的行为

- VRC ChatBox 按钮开启且 composer 输入框聚焦时，开启正在输入状态，将正文实时更新到头顶聊天气泡；正文排除行内预编辑，组字替换选区期间保持组字前正文，输入法写回后才上报新正文。语音识别的临时结果按原输入框内容实时广播。
- 失焦或关闭 VRC 开关时，只关闭正在输入状态，保留本地未发送的文字与当前头顶消息；手动清空输入框也保留头顶消息，沿用 VRChat 原生输入框的实测行为。
- 正文为空串时停止广播，保留最后一次头顶消息；有任意字符时覆盖消息，保留正文中的空白，单个空格也作为正文广播。
- 点击发送按钮或按空闲回车时，沿用 XSOverlay 原有发送逻辑，成功后清空输入框并保留正式消息；组字中的回车仍交给输入法。
- 仅正式发送允许产生 VRChat 消息通知音，遵循原有通知音设置；实时更新、输入框清空、聚焦与失焦均静音，XSOverlay 本地按键音继续遵循原设置。
- 实时广播按现有长度口径截取正文前 144 个 UTF-16 单位，不拆开代理对，本地保留全文；超出部分的变化不改变气泡，前缀变更仍更新。
- 正文实时上报侧限制为最多每 1 秒一次，间隔内合并为最新正文；正式发送沿用原有行为。
- 正文超限时，复用 XSOverlay 输入框与发送按钮之间的原有错误文字条，位置、样式与文案保持原样：`VRChat chatbox text must be 144 characters or fewer.`。正式发送仍沿用原有超限处理，拒绝发送，保留输入框内容与焦点。
- 插件补充实时广播与正在输入状态，正式发送仍由 XSOverlay 负责；发送时撤销待发旧预览，保留正式消息。

## 接入线索

- VRC 按钮为 `#keyboard-toolbar-button-vrc`，发送前同步读取其 `aria-pressed`；共享页面模块路径为 `/apps/Shared/js/api.js`。
- XSOverlay 现有 `SendVRChatChatboxText` 拒绝空白文本，且不提供 typing；可复用宿主发现的 VRChat 目标、OSC 编码器与 UDP transport。
- [官方协议](https://docs.vrchat.com/docs/osc-as-input-controller#chatbox)：实时气泡使用 `/chatbox/input(text, true, false)`，正在输入状态使用 `/chatbox/typing(bool)`。

## 同类实现

- [VRCTextboxOSC](https://github.com/I5UCC/VRCTextboxOSC) 的连续模式按固定间隔发送 `/chatbox/input(text, true, notification)`，默认配置为 1300 ms，并在清空时发送空串；项目 README 明确说明连续更新有延迟。
- [VRCOSC ChatBoxManager](https://github.com/VolcanicArts/VRCOSC/blob/main/VRCOSC.App/ChatBox/ChatBoxManager.cs) 也用 `/chatbox/input(text, true, false)`，单独发送 `/chatbox/typing(bool)`，清空时发送空串。它按配置的发送间隔运行，不把每次输入事件都直接映射成一包。
- 两个项目都证明了协议接法与本方案一致，但它们的间隔是工程取舍，不是 VRChat 官方限流契约。

## 桌面实测与限制

- 空串保留原消息，单个空格覆盖为小块空白气泡。
- typing 可与正文同时显示。单独开启 typing，或在原生聊天框保持打开且有焦点时停止输入，指示器都在约 3 秒观察点前消失；这是 VRChat 的暂态行为，没有据此增加插件保活定时器。
- 关闭原生输入框后，原生输入内容仍保留在头顶气泡；输入框关闭不会清除消息。
- 高频观察中，250 ms 间隔发送 160 条后最终显示第 158 条；100 ms 间隔发送 8 条和 40 条后分别显示第 5 条和第 32 条。序号不代表实际接收数量，原因尚未区分，不能据此确认限流、丢包或显示合并。
- 按最终采用的 1000 ms 间隔连续发送 10 条静音更新，发送端完成后，本机头顶气泡正常显示最后的第 10 条。本轮通过 1 秒一次的节奏验收；未验证每个中间报文或远端显示。另一次 1300 ms 间隔的 8 条测试也正常显示最后一条。
- 当前本机 VRChat 的 `VRC_CHAT_BUBBLE_AUTO_SEND` 设置为开启（注册表值为 `1`），未切换该设置，因此 OSC 报文与官方 auto/manual 限流分类的关系仍未确定。
- 本轮按用户要求不验证通知音。

## 实施状态与后续验收

正文实时上报采用 1 秒一次的上限，已接入页面与宿主。页面观察 VRC 开关、composer 焦点、已提交正文与原生发送状态；宿主复用 XSOverlay 发现的 OSC 目标和已安装的编码器，由帧更新合并发送。

正式发送仍由 XSOverlay 执行。发送开始时丢弃待发预览并递增版本，完成后向页面索取新状态，阻止跨 Vuplex 与原生 WebSocket 通道迟到的旧预览覆盖正式消息。

自动化覆盖页面操作到宿主消息，以及宿主广播接口到 OSC 地址与参数。浏览器测试另复用安装版 composer 与 view 模块，核对临时语音、回车发送、成功清空与超限拒绝；宿主答复仍为模拟，不覆盖 Unity 与 VRChat 的完整往返。通知音按用户要求未验证。

后续在 SteamVR 会话中验收聚焦输入、组字写回、语音、清空与失焦、VRC 开关、超限提示和正式发送；确认正式发送成功后清空输入框，头顶保留正式消息。当前实现尚未部署。

[官方 2026.2.1 发布记录](https://docs.vrchat.com/docs/vrchat-202621)确认 VRChat 存在 5 条/5 秒的 leaky bucket，并声明自动消息不计入限流，但没有说明 OSC `submit=true` 如何归类；此处保留为协议限制，不再阻塞采用已实测节奏的实现。
