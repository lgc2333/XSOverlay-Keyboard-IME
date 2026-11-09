# 聚焦动作与撤销的传输证据

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。首次前台方案的独立子问题；不是产品协议。取舍见[接线评审](foreground-acquire-review.md)，资格实验见[首次前台获取](foreground-acquire.md)。

## 安装版 Vuplex 实测

PID18712，自有400×240纹理 WebView，未调用 focus-native、产品页面、IME 档案或模式。21例包含18例 pointer/Tab（三种提交顺序×0/50ms renderer忙循环×pointer默认/pointer防夺焦/Tab），另加串行touch7→29→7。探针与 Chromium 共享进程资源；不是实际 VR raycaster 或0050租约。

| 观察                           | 证据                                                                                                          | 判读范围                                                            |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------- |
| 输入先提交，DOM标记反而先到    | 6例 pointer/Tab 中3例逆序；pointer防夺焦0/50ms、Tab50ms                                                       | 独立JS标记不能按调用相邻或DOM接收顺序替输入标注来源；不是逆序发生率 |
| 原生调用顺序已核对             | 一个逆序样本：日志119 input-call →120/121 pointer down/up返回 →122 JS mark；DOM marker127/128 →pointerdown129 | 反例不依赖跨时钟比较，也不是仅凭最终焦点推测                        |
| marker先提交及callback后送输入 | 各6例均见marker先于输入                                                                                       | 有限的一向顺序证据，不证明旧输入已排空或标记与动作原子绑定          |
| 原始TouchID未穿透              | 单触点串行API ID 7→29→7，DOM changedTouches.identifier均为0                                                   | 推翻本安装路径直接用该字段传动作ID的假设；不推广到多触点或所有版本  |
| snapshot不是完成栅栏           | 初次pointer快照未见up/click，后续才出现；一个Tab需再取快照；touchend后兼容鼠标/焦点仍可迟到                   | 不能按观察期标签归属输入，也不能凭end/一次快照确认全部副作用结束    |

21例均观察到各自已知down/up或touch start/end，未重放输入；这不是通用输入完成证明。共22次输入命令包含另一次smoke。主线程记录91次JS提交与91次callback；成功数不证明任意后续callback已排空。运行中Windows前台采样出现SteamVR，未建立全程前台不变或native WebView焦点恢复证据。

## 输入自带时间戳的新增证据

安装版公开的 `StandaloneWebView.SendDevToolsMessage(string)` 可发送 CDP Input 命令。PID36956 的独立 view 单次实测中，mouse/Tab 分别携带文档 timeOrigin+123/456ms；实际 trusted pointerdown 为122.9ms、keydown为455.9ms，而处理时刻已约10062/10072ms。输入字段穿透了实际安装版，不依赖独立JS标记。只发送一对mouse和一对Tab，没有重放或产品/系统输入。

这还不是动作ID或产品协议：所测误差约0.1ms，不保证精度、唯一性或全部输入语义；focusin 使用自己的约10062.8ms，不能从它的时间反推原动作。需要在原输入 capture 阶段保留关联并送入聚焦请求，用实际命中的节点校验目标；输入源记录还须与原STA撤销建立共同顺序。时间戳是调用者数据，不证明硬件来源，也不是输入完成回执。

自有view已Dispose/status无view；正常Steam停机后精确移除0.1.3，产品main/native/physical0.2基线未变。未证明全局native焦点恢复、callback排空或SteamVR面板恢复。

PID33512随后在原产品页接线：主线程登记旧源rev0，原STA在同裁决锁内真实撤销到1；旧实际pointer拒绝。页面仍knownRevision0、同节点focused、无focusin时，一个新pointer接受为第二期，原正常Take成功，无重击或第二批Alt；旧资格器却立即归还正常成功。PID26768只修该临时持有分支后，补齐第二期保持至原A/退格长度0→1→0，13项原始核对通过。两轮均显式release触发失焦、通知省略、旧动作在producer保留；未证明自然VR、真实通知在途、通用timestamp身份或完整0050。正常停机/精确移除均已核对，详见[接线评审](foreground-acquire-review.md)。

## CDP答复不是聚焦结果

安装公开方法 `SendDevToolsMessage(string)` 返回void，没有公开CDP答复回调；`ExecuteJavaScript`的callback只完成那次JS求值。公开remote debugging可从独立协议连接取答复，但它是初始化前的共享浏览器配置，未验证每个IWebView到调试target的稳定映射，不把它作为已选产品机制。见[发送接口](https://developer.vuplex.com/webview/StandaloneWebView#SendDevToolsMessage)与[调试接口](https://developer.vuplex.com/webview/StandaloneWebView#EnableRemoteDebugging)。

[Chromium137.0.7151.104实现](https://chromium.googlesource.com/chromium/src/+/137.0.7151.104/content/browser/devtools/protocol/input_handler.cc#702)在事件排队时等待ack，也可在没有排队时直接成功；ack不检查处理状态。它不返回聚焦目标或默认动作的原因。未处理也不等于未完成，不能反过来断言已有ack的事件仍会迟到。

一次CfT137.0.7151.119无界面对照中，正常Tab、preventDefault、handler脚本focus、忽略输入都返回相同成功答复，最终分别为B、A、C、A；忽略组没有DOM键事件，解除后新Tab仅出现一次。正常组的focusin在答复前被观察到，仍不足以为任意后续focus归因。第五个冻结隔离的预期失败：100ms内输入仍被处理，未确认renderer确已冻结；总体结果保留为失败，不用于完成合同结论。页面错误及外部请求均为0，不是安装CEF或真实游戏验收。

按实际事件自带标签关联来源不必依赖答复；标签唯一性、过期拒绝及Tab来源仍须独立验证，不能以单向发送返回或事后查询替代。

## 对方案的约束

后续PID36460把固定输入改为touch：旧源拒绝、一次新触摸接受并持有至原A/退格0→1→0，页面始终knownRevision0、同节点focused且无focusin，14项原始核对通过。仍是显式release/省略通知，未接实际Raycaster；正常停机与精确清理已核对。用户随后选择[直接注入](foreground-direct-injection.md)，不将CDP timestamp作为拟采用的产品通道。

- 页面观察标签、当前revision、回调ID和时间邻近均不能补回动作来源。新旧动作在撤销同步窗口仍可不可区分，见[接线评审](foreground-acquire-review.md)。
- callback后送输入可以作为实验的一向边界；它没有为此前待办、触摸兼容事件或原STA撤销建立共同先后，也不授权自动重放首次动作。
- 有限原STA鼠标/触摸接线已补齐旧拒绝/新首次接受及持有至原首键。复杂来源与完整编辑接管已停止；后续按[0060页面边界与重开协议](foreground-refocus-protocol.md)核对实际事件覆盖和往返，不再扩展timestamp、旧marker或Alt矩阵。
- 自有view已Dispose、status确认无view；这不等于native全局焦点恢复或callback排空。独立DLL仅在Steam正常停机后移除，部署基线以[恢复记录](release-and-recovery.md)为准。
