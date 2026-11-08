# 宿主前台租约

规则与失焦收尾见 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md)，组字宿主与写回见 [0022](../adr/0022-ingameime-host.md)、[0008](../adr/0008-inserttext-writeback.md)。本笔记只记现状、证据与待验证项。

## 现状

- 生产代码仍按单次组字会话结束并归还前台：`CompositionSession.End` → `ForegroundLease.Restore`，送键后只排空键消息。
- `HostWindow.Collect` 只追加 `WM_CHAR`，唯一消费者是送键路径内的 `WriteThroughTyped`。两条机制都未实现：字符到达不通知会话，前台随按键归还。
- 待实现：`WM_CHAR` 到达即交付完整文本（不拆代理对、不等下一次送键）；迟到提交独立交付（[0032](../adr/0032-deliver-a-late-commit.md)）；组字结束、按键批次结束与修饰键释放都不结束租约。

## 证据：过早归还前台

- 独立宿主（.NET 10，复用生产宿主、会话、送键与档案源码，加载同哈希原生库）每次送一个 A：原样归还时拼音、日语各两次无字符无组字；仅抑制归还时拼音两次开始长度 1 的组字，日语收到一个普通字符；小狼毫原样正常交付。前台获取与 `SendInput` 失败计数为零。
- 结果驱动对照（取消「仅因 `!Composing` 就 End」，字符到达即交付）：拼音 `send-entry → send-return → preedit(0→1)`，续送 Return 被接管并提交；日语 `char-arrival → delivered(1) → session-end`。原样路径仍无输出。该对照未覆盖吃键无输出、异步取消、多字符与代理对，同步 trace 也可能影响时序。
- VR Shift 保持宿主前台后，两款微软输入法都能把文本送进 VR 输入框，说明服务能连上该上下文。

## 证据：游戏内被动诊断

- 无输入段：前台、焦点与 `SendInput` 都成功，但宿主无键消息、无字符、无组字或提交回调。低级按键观察确认注入的 Down/Up 已到达（六对，回调时前台为宿主，下游返回 0）；断点在低级入口之后、宿主队列与文本回调之前，更早的挂钩或其他 TSF 处理未排除。
- 把送键后排空从键消息扩到全部已排队消息（512 条上限，无等待、重试或计时器），两款微软输入法仍无输入、`Collect` 仍空，小狼毫正常。队列暂空不等于异步 TSF 已完成。
- 迟到普通字符已有真机证据：日语缓冲 1→8，约七秒后取消 Shift 才由 `TakeTyped` 取出；另一段单个字符同样等到取消。
- 直接 TSF 固定 A：早期 `TestKeyDown/Up` 为 `S_OK`、`eaten=false`；后续同线程同上下文在真实拼音组字中取得 `TestKeyDown=true → KeyDown=true`。能消费键，但单键消费不是完成契约。
- 组字宿主吞掉全部 `WM_APP` 及以上窗口消息；尚未证明输入法依赖它们。
- 桌面键盘页能送 A 到真实 `InputManager.HandleKeypress`，但 `hasFocus=False`，未进插件宿主。

## 证据：物理输入

- 安装版 Vuplex：`NativeKeyboardListener` 读 Unity `Input.inputString`/`GetKeyDown`，`KeyboardManager` 转发到自己的焦点 WebView；与 XSOverlay 内部的 `KeyboardFocus` 不是同一焦点。
- 真实 US 档案、空 composer、宿主持前台时，实体 `A → Backspace → Left → Ctrl+Z` 的按下、抬起与 `WM_CHAR` 都只到隐藏宿主，Vuplex 监听与 WebView 送键均未记录；期间 `FrameCount`/`ListenerUpdates` 持续增长。阳性对照经原游戏 `SendKeyboardEvent` 发一次 Backspace，实际 WebView 的 down/up 各记一次。
- 独立 Win32 消息泵对照（84 组四键）：真实拼音在显式过滤泵中 15 个按下全被 `Test → Key` 吃掉、15 个抬起放行；普通取消息泵也组字但看不到按下；只包装取消息的泵产生普通字符。日语、US 与编辑键本轮无持续组字。
- 结论：只有显式过滤能确定按下与抬起的归属；下一步验证把未消费的编辑键交回页面编辑通道、普通字符只从字符结果交付。

## 已知缺口

- `TryAcceptKey`/`DrainStrokes` 不按 composer 活动资格拦旧队列；`Ensure` 可能重新获取前台。
- 提交与写回没有焦点期身份，快速重新聚焦的归属未验证，见 [late-commit](late-commit.md)。
- 页面失焦经 60 毫秒检查调用 `RequestCancel` 并最终发送 Escape；失焦与编辑取消须分开，保留默认窗口处理。
- 真实迟到提交、物理编辑键、Ctrl+A/C/V 去重、Vuplex 双路输入待验证。

## 验收

- 覆盖拼音、日语、小狼毫与 US，先确认真实活动档案再统计。
- 用户自行选中英状态；查普通字符、组字、分段转换、提交与取消。
- 查异步无输出、连续输入、多字符，代理对不拆分、不重复交付。
- 真实桌面失焦分别复现小狼毫与拼音，保留默认结果；不发 Escape，不重新聚焦也能交付旧目标。
- 查粘滞与保持修饰键、组字中切输入项、页面重建、失焦与关闭，无残留按住键、不抢回新窗口。
- 最后在提权 Unity Mono 的真实 VR 页面重测。
