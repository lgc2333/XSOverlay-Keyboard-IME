# 前台资格的接线评审

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

2026-11-09。用户接受 [0055](../../adr/0055-use-one-alt-pair-after-a-denied-focus-request.md) 的一次Alt与非事务风险；首次回点承诺后由 [0058](../../adr/0058-allow-refocus-rejection-during-revocation.md) 缩小，并由 [0060](../../adr/0060-create-refocus-requests-when-page-input-arrives.md) 选定页面收到操作的边界。原产品首A组字、Return提交与恢复已通过；真实VR又验证首个普通字符、原生失焦后拒绝旧B及已完成撤销准备的一次真实回点，见[验证边界](./foreground-acquire.md#真实vr验证)。未实施产品代码，[重开握手](foreground-refocus-protocol.md)仍待接线验证；寿命以 [0050](../../adr/0050-hold-the-host-focus-for-the-composer.md)为准。以下来源反例保留为历史证据，不再要求恢复复杂来源追踪。

## 已接受规则与建议接线

- 明确聚焦只在入口获取一次；原 STA 实际 foreground/focus 均匹配才开放送键/切档案。排队动作保存不可变来源和参数，执行前只读核验资格；失配即作废，不能通过 Ensure/Take 重新夺回。
- 原生失焦先撤销资格，再异步定向通知原文档/composer/焦点期。清除该期布局重建的保焦依据与 blur 应在同一同步入口，来源检查覆盖全部副作用。组字结束、切档案或批次结束不终止焦点期。
- 最初前台仅保存一次；退出仅当宿主仍为前台且原目标身份有效时请求归还一次。失败记录 actual 状态，不重试、不声称窗口消失。结果改按 [0057](../../adr/0057-apply-ime-results-to-the-current-composer.md) 交付当前有效聚焦的 composer，否则丢弃；结果处理不恢复送键资格。
- 接线涉及 ImeHost 的 SetComposerActive/RequestProfile、CompositionSession 的 Send/ActivateProfile/End、HostWindow.WindowProcedure、KeyboardPage.OnMessage 和 web/src/composer-focus.ts。当前 active/inactive 布尔协议及每动作获取/归还均不足；替代草案见[撤销确认与重新开放](foreground-refocus-protocol.md)，不表示产品已接通。

WM_ACTIVATE 跨 input queue 异步；失焦通知不是排队工作栅栏，因此还需每动作实际资格检查。这也不构成“检查与系统输入原子执行”的承诺。修正版观察器在PID21784捕获自然WM_ACTIVATE/WM_ACTIVATEAPP失活及WM_KILLFOCUS，DOM/game仍活动；原页面重选当前档案后仍进入Take（false、actual仍Steam）。这证实当前缺少自然失焦撤销，但没有实现或验证新的0050规则，也不是实际抢回前台的阳性。

临时单期桥接在PID12260取得反向阳性：composer先活动、实际游戏SetFocus随后到达，只启动一次资格获取；失焦在原STA撤销，仍持有旧DOM/game焦点的同档案请求被拦在ActivateProfile前，失焦后Take0。未改产品End/Restore，正常停机与精确移除已完成；未覆盖DOM定向blur、Send/ModifierUp或下一聚焦期，不能据此关闭首次主动点回要求。

PID22344随后验证已撤销且DOM inactive后的回点：一次原WebView pointer配对实际命中同一composer，第二期只Begin一次，正常拒绝后Alt插入2并获实际foreground/focus，原A/退格使长度0→1→0。该轮由显式release造成失焦，预先等待inactive、在送pointer前人工武装新期；不证明自然切走、在途撤销或产品动作关联。拼音菜单未命中，Return被守卫拒绝，未扩大为组字证据。正常停机/精确移除已完成。

新caller候选的测量也须区分完成与超时：四项隐藏窗口Win32实验中，已开始的handler在203ms超时返回后仍执行；默认同步等待发生反向回调，SMTO_BLOCK对照让内层回调超时。未发起激活，不证明Unity死锁或激活栅栏；私有队列往返、超时和清空观察记录均不能承诺取消/无迟激活。

独立观测臂已在PID10156运行：Unity主线程单次请求raw=false；observer的WM_NULL成功返回（0.6355ms），actual仍为原Chrome前台，宿主线程保留focus。获取/归还计数1/0；立即观测LastInput未变，后续status期间已变化，不能宣称全程输入隔离。官方同线程示例不形式化第三线程移交的内部通知顺序；本轮仅不支持换caller即修复，不是通用激活/输入完成、取消或产品等待协议。正常停机与DLL清理已核对。

## Alt 的不可省略取舍

候选仅放在明确聚焦入口：正常一次失败、外部前台未变、目标/页面有效、无按住键/菜单时，一批 Alt down/up 加一次请求。0/1、前台或来源变化均结束该动作，不堆叠 Attach、重试或定时器；成功后共用 0050 的同一租约。PID17608已验证原页面拼音首键及提交，探针保留原End/Restore、另存真实原前台；不能把它当成焦点期实施或真实VR验收。

[SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)返回成功插入数；批内串行不交错不是事务。返回1未公开给出部分成功的事件索引，返回2也不是处理完成。GetAsyncKeyState、低级钩子及自定义 marker 不提供修饰键所有权；补 Alt-up 也可能失败或影响用户后来按下的键。

18组合纯内存故障模型否定了“退出即回滚”“快照看到 down 才补 up”“总补 up 可恢复”的无条件承诺：已接受但尚未派发的 down 可晚于快照，补发仍可失败。即使假设部分成功总为前缀也有反例。模型不是 Windows 部分插入实测或发生率；多跑成功样本不能消除该合同缺口。

## 来源过滤的证据和缺口

真实页面源码的 headless 对照确认：inactive 状态不会 blur；window 恢复可无新动作重报 active；脚本 focusin 也可 trusted。只 blur 后同任务重建会自动保焦。内存副本同步 clear+blur 的6个案例修复了局部顺序；6种双队列交错中的来源守卫拒绝旧请求/旧 blur，但新期已由 fixture 提供，未证明完整同步。

游戏 focusId 也不是明确动作凭据：安装 API 在脚本 focus/重建时产生新 ID，0021 补丁又保留同 overlay 的旧 grant。函数片段的7项浏览器检查不是真实 VR 证明。打开、输入框按下和键盘导航的既有聚焦语义均须保留，不能静默改成额外点击或仅 pointer。

fresh 安装源码另确认：工具栏 Keyboard 命令进入无参数 EnableKeyboard；SetKeyboardVisibility(true) 无直接 focus，ToggleEditMode/布局恢复也可走同一入口，故 visible=true 不是新动作凭据。已查 HTML 无 autofocus，页面/controller 与插件 Attach/showState 无首次 focus 调用；已知请求从 input focus 事件开始。首次打开的 activeElement 生产者仍需实测，未排除浏览器保留焦点、native 或序列化回调，不能把“打开必定自动聚焦”当作已证事实。

后台写回另有6页离线对照：安装版 focus listener/API 在临时 focus 后留下请求与 Pending；恢复 other/body、跨 task 均不撤销，overlay 身份迟到仍会发送。源码也确认先保存 Pending、调用 focus 再发送。socket/背景状态为 fixture，未证明真实 native 授权，但最终焦点恢复不足以排除请求副作用；capture 对照只挡住已知同步 listener，不是已选策略。详见[迟到提交](./late-commit.md)。

新两历史模型补出此前未覆盖的同步窗口：

| 历史         | 实际顺序                                                 | native 收到的信息                    |
| ------------ | -------------------------------------------------------- | ------------------------------------ |
| 旧动作迟到   | 产生请求2 → native 撤销到1 → 收到请求2                   | 文档A / 请求2 / explicit / 已知代次0 |
| 第一次新动作 | native 撤销到1 → 产生请求2 → 收到请求2；页面尚未获知撤销 | 文档A / 请求2 / explicit / 已知代次0 |

两边 native 状态、各自本地历史和 payload 完全相同；7项纯内存检查通过。严格代次检查拒绝两者，放行新动作也会接受旧动作。原15组合/5序列明确不承诺此处可用性，双队列模型直接提供最新代次，均未覆盖此窗口。该反例只针对这些可见事实，不证明所有协议不可能，也未验证真实 VR 的 explicit 标签。

因此不能仅重标/重放待定请求、生成 UUID、看当前 DOM 焦点或接收时间。在当时“同步期间第一次新动作必须成功”的要求下，需新增共同先后事实。该体验承诺已被 0058 缩小；0060 又明确允许同步后才到页面的较早原生输入形成新请求。反例仍否定重标旧页面请求，却不再要求恢复更早输入来源调查。

安装 Raycaster 的触摸发布只填 overlayHandle、pointerId、坐标、phase、hapticTarget；PointerDown 和 KeyDown/Up 的当前 managed→native 声明没有动作标识或源时间。ExecuteJavaScript 的独立 callbackID 只绑定 JS 结果，不绑定输入。收件时补时间戳不能恢复源动作先后；native 内部队列未检查，不能扩大成所有传输均不可能。DOM目标预查询也不足：5例 headless 对照中，布局交换、同id节点替换及Tab顺序插入分别改变实际目标/节点；73项检查通过，只否定无稳定性保证的捷径，不证明真实Vuplex重排。

安装版Vuplex的21例独立view实验已补齐：6例input-before的pointer/Tab中3例DOM先处理后提交的JS marker，调用日志核对了原生提交顺序；串行TouchID7/29在DOM均为0。marker-before与callback-before各6例顺序成功，只支持所测的一向边界，不能绑定旧输入/原STA撤销。快照及touchend也不是完整副作用栅栏。详见[动作传输证据](foreground-action-order.md)；原观察标签不是动作来源，实际VR、初始化焦点和异常清理仍有缺口。

公开入口SendDevToolsMessage的输入timestamp已在PID36956穿透安装版mouse/Tab（123/456ms→122.9/455.9ms）；focusin另用当前时间，须从原输入capture保留关联。headless对照8项通过后，PID33512进一步在原Unity/原STA实接线：旧源登记→真实STA撤销→旧实际输入拒绝；页面仍代次0、同节点focused且无focusin时，一个新输入被接受，原正常Take=true并获实际foreground/focus，无第二个新点击或Alt。

PID26768仅修临时资格器“正常成功也持有”后补齐：旧源拒绝，一个新源接受；原正常Take成功并保持至原A/退格长度0→1→0，没有第二批Alt。原始日志13项核对通过，两份探针已正常停机精确移除，产品基线不变。

PID36460另补实际touch载体：旧rev0被拒，页面仍代次0且同节点focused时，一次新touch被接受；原正常Take保持至A/退格0→1→0，14项原始核对通过。两份探针已正常停机移除，三项产品基线不变、CUA已reset。

上述各轮均由显式release触发真实STA失焦，页面通知省略，旧输入由生产者保留后发出；不是自然VR切走或真实通知在途。固定标签、原输入行为、完整clear/blur隔离及检查→原生调用间撤销仍未闭合。用户随后选择[直接注入](foreground-direct-injection.md)，不采用CDP输入替换；timestamp实验保留为来源关联的有限证据。

## 下一项有区分力的验证

| 问题           | 有区分力的证据                                                                                        | 不算通过                                                         |
| -------------- | ----------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| 撤销后重开     | 依 0060 实接清理回执和开放许可，验证旧页面请求拒绝、同步期拒绝及开放后页面明确操作；覆盖重复与重载    | 重标旧请求、把较早原生输入到达误报为过滤失败；省略通知当真实在途 |
| 自然失焦寿命   | 原 STA 失焦/actual 前台与焦点、待办执行同链记录；用户选其他窗口后旧动作不重新获取；新旧文档副作用隔离 | 显式 release、没有看到消息、一次未出现旧请求                     |
| Alt 产品适用性 | 原产品首键/提交、真实VR普通首字与settled回点已过；补通知在途、VR内部切焦及菜单/修饰键                 | 只复测获前台；将输入插入数或钩子回调当完成/回滚                  |

Alt策略及非事务风险已由0055确认。复杂来源与光标/拖选/导航接管的扩展调查已停止；0057取消结果追溯，0058／0060缩小回点与来源承诺，旧页面请求和旧送键仍不得重新取得资格。当前任务是核对握手与实际输入覆盖，不再选通用timestamp载体。21:18恢复后的[正常请求与Alt守卫](foreground-acquire-guards.md)记录21:14探针入口拒绝及21例离线对照；它不证明真实获取、定向blur或通知在途。第二项仍开放，不重复既有Alt/marker矩阵。
