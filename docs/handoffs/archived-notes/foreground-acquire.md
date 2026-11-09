# 首次前台获取

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

**2026-11-09：Alt 已补齐原产品首键链，用户接受 [0055](../../adr/0055-use-one-alt-pair-after-a-denied-focus-request.md) 受限一次 Alt；首次回点承诺后经 [0058](../../adr/0058-allow-refocus-rejection-during-revocation.md) 缩小，[0060](../../adr/0060-create-refocus-requests-when-page-input-arrives.md) 改按页面收到操作为界。复杂[直接注入](foreground-direct-injection.md)与完整编辑接管的扩展已停止；结果按0057。现行方案见[重开协议](foreground-refocus-protocol.md)，[入口守卫](foreground-acquire-guards.md)记录探针额外限制。产品未实施，第二项未关闭。** 寿命规则仍见 [0050](../../adr/0050-hold-the-host-focus-for-the-composer.md)。

## 已确认的边界

- 真实 VR 首次选择拼音的前两次，原生 `SetForegroundWindow=false`、`Take=false`，前台保持 `steamwebhelper/SDL_app`，档案仍为小狼毫；真实桌面交互后，同一路径成功，实际前台和档案身份均确认。日语也有同类对照。LastInput 与前台同时变化，尚未隔离具体资格条件。
- Steam 重启、DOM 聚焦、游戏 KeyboardFocus 和 API 请求均不足以授权。菜单关闭时的 `target-unavailable` 不是档案切换结果。原游戏 BringWindowToForeground 也只调用普通 SetForegroundWindow 并丢弃返回值；PID10156真实主线程单次请求仍拒绝，见下表。
- 系统拒绝属于基线；插件失败后仍保留 composer 活动资格、未通知页面失焦，是尚未实施的 0050 失败路径。持有租约不能单独解决首次资格。
- PID21784自然切走实测：原STA收到失活及WM_KILLFOCUS、actual前台已非宿主，但DOM/game composer仍活动；沿原页面重选当前同一档案仍进入Take，结果false、actual仍Steam。证明旧资格没有随原生失焦撤销，未证明实际抢回成功；本轮用既有Alt持有探针准备初态，没有实现0050。
- PID12260临时单期桥接补齐反向对照：先等实际游戏SetFocus，再Begin一次、Alt取得前台；原STA失焦即撤销，同页面旧档案请求在产品ActivateProfile前被拒绝。整轮Take1、Alt批次1，失焦后Take0，Steam保持前台。探针未修DOM blur或实现新期，不能代表“首次主动点回即可恢复”。测试后正常停机并移除两DLL，基线匹配。
- PID22344补齐撤销完成后的回点：显式release后等待原页面inactive，一次原WebView pointer配对实际命中同一composer，第二期只Begin一次；正常Take拒绝、Alt插入2后取得实际foreground/focus，原A送入一个普通字符并退格清空。拼音菜单未命中，Return守卫拒绝未发送。本轮不是自然切走或真实VR，且预先建立撤销/失焦，不能关闭通知延迟期间的首次回点要求；正常停机精确移除两探针，三项基线匹配。
- 判定必须同时检查原始 BOOL、actual foreground 与线程 focus。固定状态检查出现 raw BOOL=false、Take=true，因为目标本来已在前台；线程保留 focus 也不代表进程持前台。

## 候选矩阵

原游戏正常/Alt/Attach 对照均在独立进程、原隐藏 HWND/STA、空闲空白页面执行；Alt/Attach 只在正常一次失败且前台未变后试。产品 main/native 哈希未改，临时资格诊断已移除。

| 候选                     | 已验证                                                                                                       | 反例、未解项与下一验证                                                                                                                                            |
| ------------------------ | ------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 正常一次                 | PID31212 raw BOOL/Take=false；Steam 前台不变，自队列及16秒后仍未激活                                         | PID35536 无输入、无泵、无等待的立即第二次请求也 false，两次同 tick31326187；排除该样本“只要再调用一次”。仍需真实 VR 同初始前台的冷开/桌面交互对照，不支持产品重试 |
| 原 HWND 换 caller 线程   | PID10156：主线程向原STA HWND单次请求 raw=false；独立WM_NULL成功后actual仍为原Chrome前台，未执行归还          | 仅此样本不支持换caller即修复；资格按calling process，无主线程特权保证。独立observer移交不是通用激活完成合同；四臂同环境控制仍未运行，不据此断言普遍无效           |
| 成对 Alt 后一次          | PID33024 基线 false；SendInput=2、钩子见 down/up，下游返回0；随后 actual foreground/focus 均为宿主，归还成功 | 自队列、钩子及 async down=0 都不是输入处理完成或无副作用证明。需真实 VR 首键、自然失焦、原前台菜单/修饰键检查；部分插入、真实 Alt 交叠未验                        |
| 短 AttachThreadInput     | PID22760 attach=true 后已获前台，早于第二次请求；detach=true 后仍持前台                                      | 唯一归还请求 false，原 HWND 仍有效。不能说 detach 即恢复，也不能只归因第二次请求；会重置键盘状态。下一对照为 attach-only 与正常组及归还，不只复测获取             |
| AllowSetForegroundWindow | 普通双进程 eligible grantor 持前台，grant=true，worker 一次请求成功                                          | 授权方立即归还 false；无授权首轮被 exec 抢前台，已判无效。没有真实 Unity 的可控 eligible grantor，先解决此前提；失败方不能自授权                                  |
| 显示/点击原宿主          | 独立物理路由实验中显示原 HWND，控制工具点击后获前台、原页面切 US 成功，见[宿主租约](./host-lifecycle.md)     | 混合显示、样式变化与桌面点击，不能归因纯显示；需只显示/真实点击对照，暴露桌面窗口也不等同无感 VR 恢复                                                             |

另一次全新 PID19828：原 BOOL/Take=false、Steam未变；Alt pair 后一次请求成功并保持前台，经原页面/原 SwitchProfile 在原 STA 依次切拼音、日语，读回完整身份且页面 current 一致。原 Activate 恢复小狼毫 Type/lang/service/profile/HKL 完全相等，再一次归还回原 Steam。未读写模式，页面空白。两次档案切换共用一次资格，并非两次冷开；探针另存真正原前台，不能当作产品自然 0050 生命周期或真实 VR 手势验收。

**新增首键样本 PID17608：** 正常 Take 拒绝后，Alt pair＋一次请求取得原隐藏宿主前台/focus。原页面切微软拼音后，原 SendKeyboardEvent 首 A 触发真实 preedit；Return 产生 utf8Bytes=1 的 commit 并结束组字；固定 Backspace 清空唯一测试字符。原 Activate 精确恢复小狼毫，再一次归还真正原 Chrome HWND/PID/TID/focus，菜单/async down 快照均为0。首轮准备曾因外部前台/LastInput变化中断，guard拒绝A未发文字；第二次准备完成上述链，不算两个冷开样本。

此探针没有抑制产品 Restore、修改 End 或取消提交：原 profile Acquire 保存当时已在前台的宿主，原 End/Restore 仍归还宿主自身；探针另存真正 Chrome，显式 release 时归还。因此该阳性补齐资格到原产品首键，不等同0050完整焦点期或真实VR输入。测试后正常停机，精确移除资格探针并核对main/native/physical0.2基线。

普通完整性进程的 Alt/Attach 阳性仅作方法对照；外部辅助程序对真实宿主正常请求已成功，未进入候选。不同进程/时序仍限制因果和成功率结论，一次 Attach 归还失败也不证明它普遍无法恢复。

## 已选策略与待验收

用户已接受 [0055](../../adr/0055-use-one-alt-pair-after-a-denied-focus-request.md) 的全局输入与不可保证回滚取舍，不再穷举其他候选。接线限于0050明确聚焦入口，不能放进每键Take/Ensure；失焦先撤销、旧工作不得重新获取，仅宿主仍在前台时条件归还最初目标一次。真实桌面恢复路径仍保留。

[接线评审与验收缺口](./foreground-acquire-review.md)记录 Alt 失败合同和历史动作来源反例。0058 已允许同步期间拒绝并要求重新操作；0060 允许同步后才到页面的较早原生事件算新操作。旧页面请求仍作废，不能将有限模型通过写成实际协议通过。

自然切走、旧档案请求拒绝、原产品首键和撤销完成后一次回点已有证据；剩余关键验收是实际VR冷开/首动作、完整失焦与撤销确认接线、按0060区分旧页面请求和新到页面的操作，以及菜单/修饰键观察。不再为Alt资格穷举矩阵。SteamVR原面板状态未知，未盲切；产品代码未改。

## 真实VR验证

最新 23:42 样本：normal 一次失败，一批受限 Alt 后实际宿主 foreground／STA focus 成立，日语组字与候选出现。Return 没有进入宿主请求，不能记成提交通过；断点和 11／13／0 计数见[回车证据](return-key.md#证据)。[23:50 收尾](release-and-recovery.md#2350-日语-return-复测收尾)已移除两探针、五基线匹配，原输入法恢复未确认。

2026-11-09，用户在头显内操作，临时桥保留原生编辑，产品代码未改。

| 有限场景                                                                                    | 结果                                                                                                                                          |
| ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- |
| PID30852，首次点击 composer 后按虚拟 A                                                      | 原 Take=false，外部 QQ 前台未变；一批 Alt 插入2后实际 foreground/focus 匹配，原 WM_CHAR/TakeTyped 长度1，用户看到字符。不是已证明的真实组字。 |
| 同期切到桌面 Codex 后，不重新点击 composer，只按虚拟 B                                      | 原 STA34540 先观察到失焦并撤销，旧 Send 在产品前被拒绝；用户确认没有新增字符或组字。                                                          |
| PID14020，自然失焦后完成 release、同节点 blur/inactive，再由用户点回一次，连续 A、Backspace | 第二期正常 Take=true，无第二批 Alt；原 A 交付，Backspace 走浏览器原路径，同节点末态长度0。用户确认首次点击有效；回复 Codex 后才再次失焦。     |

两轮均精确恢复初始档案，因用户已选其他窗口而跳过归还；正常 Steam 停止后移除当轮两只探针，五项基线哈希一致。第二轮一个并发只读 status 客户端发生 `SyntaxError: Unexpected end of JSON input`；另一 page-snapshot 及原日志完整，未重放输入。

回点验证针对已完成撤销准备的状态，第一轮打开也由用户确认可见；第二轮曾由控制接口创建页面，不能当成独立冷开证明。切走是桌面 Codex 交互，不是 VR 内部焦点切换。真实通知在途、通用动作来源、定向 clear/blur 和完整0050仍未闭合。

## 判读依据

- 快照、原生返回和前台事件分别记录；归还 true 也可能短暂看到前台0，须核验后续身份。Restore 将拒绝记成 previous window is gone，不能用此日志证明窗口已消失。
- [LockSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-locksetforegroundwindow)只明示用户 Alt 放行，未承诺模拟 Alt 绕过所有条件；[AttachThreadInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput)共享输入并重置状态；[AllowSetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-allowsetforegroundwindow)要求调用方本有资格，后续输入可撤销授权。
- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)描述calling process资格；[SetFocus](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setfocus)要求目标属于调用线程消息队列。[Microsoft实现解释](https://devblogs.microsoft.com/oldnewthing/20161118-00/?p=94745)另述跨队列激活通知异步；这不提供主线程资格或输入完成保证。
