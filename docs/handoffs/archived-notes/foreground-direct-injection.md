# 直接注入聚焦动作

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。用户曾选择通过 BepInEx/Harmony 与页面 JS 调查来源接线；后已停止扩展复杂来源与编辑接管方案，产品未实施，完整适配责任未采纳。本文保留历史候选与证据，不作为恢复实验的指令。结果规则现为 [0057](../../adr/0057-apply-ime-results-to-the-current-composer.md)；0050 的失焦安全和 [0055](../../adr/0055-use-one-alt-pair-after-a-denied-focus-request.md) 的一次 Alt 保留。现已按 [0060](../../adr/0060-create-refocus-requests-when-page-input-arrives.md) 缩小来源边界，后续以[撤销确认协议](foreground-refocus-protocol.md)为准，动作证据见[传输记录](foreground-action-order.md)。

## 历史接线候选（未落地）

1. 仅拦截实际键盘 WebView 的输入生产者。在提交前保存文档实例、逻辑 composer、动作 ID、撤销代次及参数；与原 STA 撤销共用短状态裁决入口。执行外部 API 时不持锁，接收消息时不能重编号为当前动作。
2. 同一个页面函数接收记录、检查当时实际目标、执行所接管的聚焦并返回原记录。已经 focused 的 composer 也能显式申请新期。若保留独立原生点击，它的默认动作不能因为旁边有这次 JS 调用而被视为已关联。
3. 原 STA 校验后执行一次正常获取，必要时按 0055 执行一次 Alt；成功后持有整个焦点期。真实失焦先撤销资格，再定向清除原期保焦依据并 blur；旧通知的检查须覆盖全部副作用。普通 focusin、窗口恢复、布局重建不自行授权。
4. 关闭、重载、重复聚焦消息及异步资格回复须处理原动作的安全边界，旧请求不能重放获取。文本、组字和候选结果现按 0057 处理，不沿用原 composer 交付要求。

## 已核查入口

安装版 `Raycaster.OnPointerPress(PointerPressEvent)` 进入 `BeginCapturedPointerPress(PointerPressEvent)`，保存 `CapturedPressId`；`SendWebViewPressDown()`、`UpdateCapturedWebViewPressMotion()`、`CompleteCapturedWebViewPress(AngularPressDecision)` 负责按下、移动及结束，另有取消路径。完整适配要覆盖同一次按压的整个生命周期，不能只替换 down、留下原生 up/cancel。

`KeyboardInputFocusManager.SendKey(string key, KeyModifier modifiers)` 会先 SetFocused，再发 KeyDown/Up；可拦截导航，但所有实际 Tab/打开入口仍须在运行中确认。页面已有 `IWebView.ExecuteJavaScript` 与 `KeyboardPage.Call` 桥，可复用；安装版 pointer/key ABI 没有通用动作 token，现有 hover/haptic 消息也不是原点击的凭据。

## 新的可行性证据

2026-11-09的一次性 headless Chromium 协议夹具通过16项检查。点击保留 selection 3/3、拖选保留2/7、Tab/Shift+Tab到相邻输入；完整手势各消费一个记录，touch兼容事件不重复消费。旧代次在页面仍报告旧代次时可拒绝，fresh source 可接受。反例也通过：清除未观察槽后，迟到A被标成B；未注册producer先到也被标成当前槽。因此缺失 primary 后必须保持占用，不能靠超时、重载或清槽推进。这是夹具刻意制造的顺序，不是实际Vuplex迟到测量。

这只支持“独占所有 primary producer＋不可变记录＋串行 mailbox＋有缺失即停”的候选，不是自由运行的旁路 marker。它仍未证明 Vuplex 队列、真实 VR producer 覆盖或自然失焦；Tab 的 `keydown.target` 还是旧控件，后续默认 focus 不能仅凭 primary token 归因。

## 输入收口与回收

安装版 prefab 的 `KeyboardEnabled`、`ClickingEnabled`、`HoveringEnabled`、`ScrollingEnabled` 可关闭自动输入；键盘开关在 Update 才应用，赋值不是即时屏障。标准硬件键盘经托管 KeyboardManager 进入公开 KeyDown/Up 或 SendKey，XSOverlay 的显式 Raycaster/SendKey 不受这些字段限制；须在共同托管入口拦截，不能只关字段就宣称独占。

单实例 Dispose 先设 IsDisposed，再调用 void native Destroy，没有销毁完成或队列清空确认。新实例使用 GUID，旧实例从消息路由移除，可隔离旧回调身份；不证明全局焦点副作用已结束，也不能用于同实例 reload 清槽。带完成 Task 的 TerminateBrowserProcess 终止共享 Chromium，影响所有 WebView，不作为本项恢复策略。

Tab 的默认导航会运行 focusing steps，但页面没有原 keydown 的因果字段或默认动作完成通知。eventPhase、微任务及下一任务都不能补成该凭据。因此这不仅是缺失事件的恢复问题：即使 primary 完整到达，后续 focus 仍需适配。

公开依据：[Vuplex手工键盘输入](https://support.vuplex.com/articles/keyboard/)、[Dispose](https://developer.vuplex.com/webview/IWebView#Dispose)、[共享浏览器终止](https://developer.vuplex.com/webview/StandaloneWebView#TerminateBrowserProcess)、[Tab默认动作](https://w3c.github.io/uievents/#event-type-keydown)、[微任务检查点](https://html.spec.whatwg.org/multipage/webappapis.html#clean-up-after-running-script)。本轮只读核查安装托管实现与这些合同，无桌面操作或新输入实验。

## 未采纳的接管范围

新增[显式编辑临时验证](foreground-injection-spike.md)：同源同步focus/selection在13个固定原生对照中一致；独立边界6例仍有5例不一致，包括控件外拖选、RTL逻辑箭头和focus失败却回报资格。仅证明基本操作可适配，没有完整行为或实际CEF／VR结论，不能据此采纳产品责任。

六例 headless Chromium 对照、8项检查通过：原点击得到 selection 3/3，`focus()+dispatchEvent(click)` 留在 8/8；原拖选得到 2/7，JS 派发仍 8/8；原 Tab 到 B，JS keydown/up 仍在 A。因此“可注入”不等于“保留原生默认行为”。这是浏览器对照，没有运行 Unity/Vuplex 或真实 VR。

当时比较了两个范围，后者未获采纳：

| 范围                               | 当前结论                                                                                                          |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| 注入来源观察，继续依赖原生编辑     | 单 primary 的有限关联可行；缺事件推进与默认 focus 因果仍未闭合，不能直接进入产品实施。                            |
| 注入函数实际执行完整聚焦与编辑动作 | 可拥有自己的来源；坐标查询已有标准API候选，仍需适配光标、拖选与导航。未制作完整适配，六例简单替换不能充当其证明。 |

该责任范围未被采纳，不再为此追加确认或原型。[安装模块原生编辑基线及坐标API](foreground-editing-baseline.md)仍作证据：Tab进入会全选，禁用按钮被跳过；标准caretPositionFromPoint四例与原生点击一致，但不等于完整适配。原run的18/21和独立滚动修正9/9分别保留，未把原失败改成全通过。光标、拖选及取消、触摸兼容事件、Tab/Shift+Tab均没有因此被允许简化；不能默认为点击末尾、丢弃拖选或要求再点一次。

真实通知在途、旧 clear/blur 隔离、原 STA 自然失焦与首次新动作尚未闭合；停止扩展不等于这些边界已获证明，也不能写成“只剩产品实现与真实 VR 验收”。

责任选择限定为来源方案需要接管的光标定位、指针／触摸拖选和聚焦导航，不表示重写文字插入、删除或撤销。[0054](../../adr/0054-write-ordinary-text-through-host-results.md)已规定未消费编辑键与快捷键交浏览器默认处理；临时箭头／Selection.modify对照没有修改这一规则。若最终接管Tab导航，须明确评审它与该规则的关系，不从“同意注入”推导已接受例外。

指针源的冻结位置已进一步定位：实际Begin先进入设备FIFO，之后才命中和发送；应在入队前保留原动作／撤销代次，不能到Raycaster或Vuplex处才登记。目标须等本帧实际命中再绑定而不刷新代次；设备实例＋PressId、Prepare的SetFocused和后续fallback均须覆盖。见[来源链](foreground-pointer-source.md)，不是已经实施的Harmony补丁。
