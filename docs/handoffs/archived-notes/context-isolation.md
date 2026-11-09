# 多上下文隔离、候选归属与回收

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

本页保留原目标隔离与回收的调查证据。共享上下文全范围清理会误删新组字的发现仍有效；但用户因实现复杂度放弃 0053，现行结果规则改为 [0057](../../adr/0057-apply-ime-results-to-the-current-composer.md)，不再追溯迟到结果或要求按目标保留文档。前台安全仍遵循 [0050](../../adr/0050-hold-the-host-focus-for-the-composer.md)，后端与线程划分未选择，写回边界见 [late-commit](late-commit.md)。

## 原生隔离方向的调查记录

下表为旧隔离路线的对照与当时缺口，不是继续实施的指令；当前取舍见下一节。

| 方向                                      | 已有证据                                                | 当时未闭合项                                                    |
| ----------------------------------------- | ------------------------------------------------------- | --------------------------------------------------------------- |
| 单上下文，按 composition 身份固定私有范围 | 合成隔离与有限回收通过；真实拼音延后旧清理仍保留新组字  | 自然迟到、输入期绑定、普通字符来源、其他输入法与物理编辑键      |
| 单 STA，每个输入期独立窗口与文本存储      | 合成提交与显式 ACP 路由隔离通过；真实拼音旧提交保留来源 | 真实 ACP 组字、未知候选文档处理、输入模式保持、旧上下文有界回收 |
| 每个输入期独立 STA                        | 合成 UI-element 事件隔离；显式档案选择能匹配新线程      | 真实候选与获焦后的档案、用户模式保持、线程与上下文回收          |

三条方向都不能以组字结束或单个异步标记作为回收证明。

## 已更换的目标寿命决策

- [0053](../../adr/0053-retain-the-native-document-for-the-composer-target.md) 曾要求跨失焦保留目标文档并交付原目标，真实拼音 A→B→A 支持过此方向；现已由 0057 取代，原因是实现复杂度，不是否定该实验。
- 结果按当前有效聚焦的 composer 处理，否则丢弃；不再维护旧结果的目标或焦点期身份。原生清理、资源释放和旧送键撤销仍须安全，文档或线程如何销毁尚未决定。
- 当前 `PageUnavailable` 混合查找失败、WebView 替换与导航开始，不能直接证明原生对象已可销毁；隐藏也不是销毁证明。
- 原 shim 与回调模板的隔离工厂对照通过六场景、19 项检查：候选以不复用的 `user` token 路由三类回调，先移除映射再销毁 A，不影响 B；保存的旧 token 不会误投新 C，UTF-8 与候选复制正确。生产托管包装仍限定单上下文。重入仅走独立保存的 trampoline，未验证真实 TSF、并发退休或 shim 内重入析构，映射移除不是回调完成屏障。

### 安装版寿命信号

- 安装版 `Assembly-CSharp.dll` 的 `XSOverlay.WebApp.OverlayWebView.CleanupWebView()` 先清空当前 WebView，再对已发布旧对象发出 `WebViewReplaced(old)`，最后请求销毁 prefab。旧引用此时可能尚未 `IsDisposed`，因此只检查 disposed 会漏掉已失效目标。
- owner 的 `OnDestroy()` 会抑制上述替换事件；应用退出又直接 Dispose。相同 `IWebView` 内的 `LoadUrl` 也能更换 document。owner 销毁、WebView 替换与页面导航须分别验证，单个事件不覆盖全部寿命。
- 键盘开关的关闭分支只释放焦点并停用对象；`HandleOverlayLifecycleInactive()` 也只释放输入焦点。`keyboardClosed`、`ownerClosed` 标签不证明文档销毁，隐藏仍是可保留目标的候选边界。
- 安装页面在 `UpdateKeyboardLayout` 时保存值与选区、移除旧容器并创建新输入框；`UpdateKeyboardState` 单独只更新状态与样式类。真实布局消息可作为重建触发来源，但不认证新节点身份；公开 `setLayout()` 能直接重建，当前整个安装应用目录未找到其实际调用者。未看到 undo 或 focusId 转移，不能把可能的原生重建行为归为插件缺陷。
- 一页四场景离线加载实际安装模块后，布局更新和直接 `setLayout()` 都替换节点、保留值与反向选区，状态更新保留节点。原 handler 先注册时，后注册的 socket capture listener 已看到新节点；MutationObserver 更晚执行。原代码重连换 socket 后，旧 listener 不再覆盖新消息。传输是 EventTarget mock，消息不可信，未验证真实 WebSocket 或 Vuplex；不能据此获得重建前节点或目标授权。
- 保留先前输入框引用的一页六场景对照中，普通布局与重连后的布局能在 MutationObserver 前关联，任意替换和直接 `setLayout()` 会失效。同栈替换再布局仍会关联最终节点，但原对照把两节点的值与选区设成相同，不能据此判为目标误接。后续异值对照确认：新节点继承断开旧节点的 `OLD-ABCDE` 与 `[1,4,forward]`，而非替换节点的 `NEW-uvwxyz` 与 `[5,8,backward]`；安装版保存来源是私有 `state.VoiceInput`。这证明该路径的数据延续，未认证完整目标寿命；原模块与候选未改，初始身份和消息仍由实验给定。
- 当前安装版 `PageLoadScripts` 在 `HandleLoadFinished` 中先报告 Finished，再提交列表内脚本；[官方说明](https://developer.vuplex.com/webview/IWebView#PageLoadScripts) 也未承诺早于页面脚本执行。因此不能把该入口当成 document-start，更不能用测试浏览器的早期注入代替产品执行时机证明；其他集成入口尚未由此排除。
- 被动寿命探针在真实游戏记录 24,562 帧，失败与丢记录均为零；正常隐藏／显示保留 owner 与 WebView 身份，disposed 为 false，active／lifecycle 随显示切换。未出现 Ready、Replaced、导航或销毁事件，退出前已停止观察，不能据此验收这些回调。下一步分别核对显式旧对象事件、owner 销毁与同 WebView 导航，再验证布局来源和节点关联；不靠查找失败或同 ID 猜测。
- 后续对已确认、空白且空闲的原键盘只调用一次公开 `Reload()`，刺激与独立被动观察均记录同一 owner／WebView 的 Started→Finished，disposed 为 false。Finished 后独立采样仍未确认插件脚本，隐藏再打开后确认；不能把该采样解释为永久失败。正常隐藏仍保留对象，观察在退出前停止，未验收实际替换或销毁。
- 当前 `KeyboardPage` 没有文档世代：Started 只清确认与候选区域，旧脚本回调若随后返回 `missing`，仍可重注入并重放旧表达式；固定 ready 消息也只核对 WebView 引用。安装版 managed 层按 callback ID 分发，不在 Reload／Started 中清表，但 native 是否取消或排空旧回调仍未知。上述 Reload 没有在途业务回调，不能证明迟到已发生，也不能充当回调屏障。
- 直接编译完整 `KeyboardPage` 的五组离线对照确认上述条件分支：Started 后手动交付旧 `missing`，会注入并原样重放一次；旧成功值不重放，第二次 `missing` 不再重试。同 view 的固定 ready 重新确认并发送五个当前状态，其他 sender 被拒绝。fake 没有执行 JavaScript，16 个源码哈希不变，仍须用真实无害回调判断 transport 时序。
- 一次真实 Reload 对照在调用前提交三个固定只读标记，均在同一 Unity 线程、Started 通知前返回旧 `performance.timeOrigin`；Finished 后第四个标记返回不同 origin，同 owner／WebView 未 disposed。本次没有跨 Started 的迟到样本，不能建立通用回调屏障，也未触发产品旧 `missing` 或 ready；上述条件风险仍未证实发生。
- 新协议夹具在同一浏览器 Page 的三个真实 document 中通过 23 项检查：旧请求在新文档执行仍可回传原 ID，因此 ID 不能绑定执行文档。先查询实例标记、再于执行时核对后安装，可拒绝旧初始化和旧业务；业务缺失不注入或重放，新文档另行确认后可用。控制端为 fake，未执行生产 bundle、Vuplex、原生目标或焦点授予；首次查询与 ready 本身不证明来源或当前存活。
- 后续完整、未修改的生产 bundle 对照通过五场景、60 项检查：旧标记在任何模块执行前被拒绝，未注册 listener／observer，也未产生 ready、API、样式或后续桥消息；新文档安装后，focus、ChatBox、选区取消与候选栏重挂均有阳性。21 份输入哈希不变；仅证明隔离浏览器中的前置守卫，原生接受仍为 fake，未验证真实 Vuplex、焦点资格或结果归属。
- 守卫与既有当前选区写回候选衔接后，同文档重新聚焦可保留新组字并交付给定旧结果，撤下显示后的原生撤销／重做有效。另两场景、57 项检查确认：导航后的旧初始化与旧结果不触及新文档，同 ID 新节点拒绝旧来源；新文档独立安装后可正常写入并撤销。原生归属仍由 fixture 给定，composer 为临时转换版本；本轮后台前提失败，未运行后台编辑，不构成真实 Vuplex 或完整寿命验证。

## 隔离证据

- 合成：两个隐藏不可激活窗口各建上下文，回调带独立 user 标记，用 `EnumDocumentMgrs → GetTop → GetActiveView → GetWnd` 匹配；旧上下文提交 `a` 时新上下文保留组字 `b` 并独立提交；全范围清理在各自上下文产生额外隐式组字。
- 真实焦点转移：两屏幕外窗口的上下文都激活，来源 2 有长度 1 的组字时先聚焦来源 1 送固定 A；来源 2 结束回调时前台与键盘焦点已是来源 1，提交仍按来源 2 交付 `a`；来源 1 随后失焦也提交自己的 `a`。旧提交先于新组字开始，未验证重叠。
- 结束通知早于 `WM_KILLFOCUS`，焦点转移内可重入：资格不能只读缓存，发送前须核对实际前台与键盘焦点；`SendInput` 与核对之间仍有竞争。
- 日语组字在焦点转移前自行结束；小狼毫只交付普通字符，不能验证不提交的基线。
- 资源：30 份额外未激活上下文增加 30 个 USER 对象、0 个句柄与约 88–100 KiB 私有分配；释放后 USER 回基线，句柄与私有分配未回。这不是真实输入法上下文成本。

## 显式文本存储：替代验证方向

- 复用[微软 TSFAPP](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/Win7Samples/winui/tsf/tsfapp) 的完整 `ITextStoreACP` 与锁处理，每个文档绑定自己的 EDIT HWND。真实框架异步请求在切到 B 后仍调用 A 的 `SetText → InsertTextAtSelection`；随后投给 A 的多字符和代理对得到 A=`alpha🙂xy🙂`、B=`b`。两种无锁写入均返回 `TS_E_NOLOCK`，全程隐藏且前台不变。
- 证明已排队、已绑定 context 的操作与指定 HWND 的字符保留文档来源；不证明输入法在切换后才生成的结果会选旧文档。`EN_CHANGE` 是内容变化，不是提交；复用同 HWND 仍无输入期身份。原型用显式 `Pop` 收尾，没有自动回收证明。
- 可见原型实按 A／B，普通字符分别留在 A／B；拼音档案回读匹配，按 A 时实际 TSF 文档也匹配 A，但只有 `WM_CHAR`，没有组字回调。不能归因输入模式或宣称真实 TIP 来源已验证；未读或切模式，试后回读确认恢复初始档案。
- 下一步对照真实输入法 A→B：记录实际 store／context／HWND，不查模式、不按当前目标补身份；同时验证提交抽取、失焦默认行为、候选与输入连续性。该方向与私有范围方案并行，尚未选架构。

## 候选归属

- 原生监听订阅共享线程管理器的 UI-element source，未按文档过滤：真实拼音四段候选中两个上下文都收到同一 Begin、Update、End，而观察器的实时归属查询分别匹配窗口 1、2。
- C ABI 没有候选 ID；ID `0` 在 End 后复用并可转属另一窗口，旧 End 时前台已换窗口，按当前焦点猜归属会误路由。
- `GetDocumentMgr` 可正向归属，但微软 SampleIME 返回 `E_NOTIMPL`；UI-less 内容可能到首次 Update 才就绪，目标文档可随更新改变，须每次重新确认。End 只带元素 ID，只能依据先前确认的关联并在 End 清除；没有 ID 永不复用的保证。
- 日语与小狼毫本轮无候选，其归属接口待采样；多元素重叠未验证。

## 线程作用域

- 同 STA、同管理器两个监听器共享事件；两个独立 STA 各收本线程的事件（6 进程合成对照）。支持继续调查独立线程，但不证明真实候选、模式保持与回收。

## 收尾与回收

- 48 个独立子进程：`UnadviseSink` 不取消请求；非最后一次 `Deactivate` 不排空；`Pop(TF_POPF_ALL)` 释放且不执行；最后一次 `Deactivate` 在返回前重入执行；连续两次 `Deactivate` 时第二次执行并释放全部。异步标记不覆盖回调内追加的请求。
- `ITfEditSession` 允许请求不执行（授予锁前上下文销毁，或请求方服务先停用），资源须在析构清理；`RequestEditSession` 只保证按序与同步优先，不是完成屏障；`Deactivate`、`Pop`、`UnadviseSink` 都不建立未来结果的屏障。
- 现有原生库：泵到提交再销毁只交付一次；直接 `iime_destroy` 不交付；`iime_set_activated(0)` 停用返回前不提交、随后交付一次（会终止组字，不能代替默认失焦）；A 待提交、B 有 `b` 组字时销毁 A，A 不交付而 B 保留。
- 原生析构依次停用、解除通知、移出上下文、停线程，无待处理请求记录；COM 引用不保活父对象。原型保活到退出不是产品回收。
- 释放内存安全、已知请求完成、未来结果完整是三个问题；不能按组字结束或单个标记回收，也不能用固定等待推断。
- [`AssociateFocus`](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfthreadmgr-associatefocus) 明确不增加关联 document manager 的引用；焦点关联不是保活。独立请求记录保活 context，也不能据此推断仍在文档栈或最终会执行。
- 只释放应用引用的三进程合成对照：请求只持有 context／range 时，首请求未执行、未析构；再持有 document manager 时，首请求与其追加请求均执行，文本长度 5／10、两次 `GetDocumentMgr=S_OK`、两个记录均析构，但 owner／状态／HWND 仍在。两组都到 3 秒观察截止，文档枚举已回基线仍不足以证明回收；这不是永久泄漏证明。独立 `Pop` 对照取消未执行请求并释放全部对象；不能把取消当提交保留。全程隐藏窗口，前台不变；未验证真实输入法或任意未来请求。
- 早期逐层删减与完整 ACP 对照在 3 秒截止时仍保留 owner／HWND；不能据此判定永久泄漏。后续默认存储对照发现：从未 `Push` 的 context 调用 `Pop` 返回 `E_FAIL`，owner 引用仍为 2；明确 `Push → Pop` 后引用变为 1，应用释放立即销毁 owner／HWND。该结果纠正无栈对照的解释，未验证 ACP 的完整收尾。

## Transitory 结果与有限回收（2026-11-09）

- 父 document 上的 `ITfTransitoryExtensionSink` 可直接读取子 context 的 result／composition 范围；只在 context 上订阅失败。回调绑定不可变目标，当前目标改为 B 后，已排队的 A 写入仍交给 A。
- 同一 STA／manager 的 100 对独立 A/B 文档全部通过：1,200 个已知请求全部执行并析构；1,000 次回调覆盖双范围、仅 composition、仅 result、双 null；400 次 result 消费无串写或重复，200 个指定旧 HWND 的本地字符仍交原目标。
- `pfDeleteResultRange=FALSE` 保留 result；随后 TRUE 只删除该 result，活动 composition 保持。回调最大嵌套深度为 1，已检查 HRESULT 与断言失败均为零；不能推广为其他回调不会重入。
- 四组有限合成选区对照中，回调 cookie 下的 `GetSelection` 与 composition ACP 起点相减，正确得到 `中😀文` 的首／中／尾光标和反向代理项选择区。TRUE 删除三单元 result 前缀后，相对端点、活动端与子 context 身份不变，12 次回调及完整回收通过；未验证真实 TIP 光标。现有预编辑 ABI 不传活动端，产品传递仍未闭合。
- 另三组合成长度对照用 clone 范围分块读取：65 单元 result 与 97 单元 composition 跨 32-WCHAR 代理对边界仍完整，31／32／33 单元对照也通过。72 次 Windows 严格 UTF-8 往返保持原文，原范围未移动；结果各消费一次，组字与相对选区保持，12 个请求及全部对象释放。旧 32 单元限制属于探针，不是 TSF 限制；本次未验证真实 TIP 或产品 ABI。
- 已知批次按实际请求／回调／字符计数及文本快照完成后，disable → unadvise → `Pop` → 释放全部引用，200 个 owner／sink／HWND 均在 manager 停用前销毁，每对 owner 引用归零。旧探针遗漏 compartment／source／子 context 引用，不能用于判定整期回收。
- disable 不是排空：设为 `TF_TRANSITORYEXTENSION_NONE` 会在返回前销毁尚未执行的 child 请求，回调为零；disable 后再 pump 也不恢复结果。只释放应用引用而不 `Pop`，已知结果完成后仍留一个 owner 引用，停用及 COM 退出也未释放。
- 按当时 0053 的实际目标销毁语义，另两组隐藏窗口对照保留活动组字与未消费结果后直接收尾，未显式结束组字。两组各释放两个 owner／sink／HWND，均在 manager 停用前归零；增加的排队写入未执行即析构，另一个文档仍能更新组字并只消费自己的结果。这只证明有限合成目标的取消与释放，不覆盖真实 TIP 保活、产品销毁信号或回调安全；不能用于普通失焦。
- 上述压力与回收结果只覆盖合成生产者的有限批次。公开契约尚未找到默认失焦的最终完成信号：[`OnEndEditTransaction`](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfedittransactionsink-onendedittransaction) 结束单个事务；[`OnEndCleanupContext`](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcleanupcontextdurationsink-onendcleanupcontext) 完成停用触发的清理通知，并不保证普通失焦后的输入全部交付。不能用这些事件、空 result 或固定等待决定退休。

## 真实 Transitory 输入（2026-11-09）

- 两个可见窗口各绑定独立 HWND、父 document 与不可变目标；同一 STA 的正常消息循环只运行一条 `Test → Key` 消费链，再翻译未消费消息。每次获焦回读 TSF document 身份；选择档案后回读完整身份，不读或切输入模式。
- 拼音 A 输入 `ni` 后只报 composition；点击 B 走默认失焦，未强制提交或取消。B 输入 `h`、Space，result `和` 只交 B；再点回 A，旧 result `ni` 才交 A。共 7 次 transitory 回调、2 次结果交付，没有普通字符或串写。它证明保留的真实 TIP 文档仍能交付原来源，不证明复用同一 HWND 的新输入期隔离。
- 日语档案回读匹配，A 的 A 产生 `WM_CHAR a`，B 的 Shift+A 产生 `WM_CHAR A`；字符在各自抬起前立即到达，各交原 HWND 一次，没有组字样本。初始小狼毫档案下另有普通 A 与 Alt+A 的 `WM_SYSCHAR` 对照，不能据此推断任意跨期翻译来源。
- 两轮先恢复并核对初始档案，再明确关闭临时应用；每轮 2 个 owner／sink／HWND 全部销毁，owner 引用归零，已检查失败为零。明确关闭不等于默认失焦的自动完成条件。
- 尚未覆盖提权 Unity／真实 Vuplex、日语组字、未知候选文档、跨输入期普通字符及任意延迟结果后的有界回收；本轮未选架构。

## 有限请求回收实验（2026-11-09）

- 同一 STA、同一 context 重复 400 次：同 edit 与跨 edit 各 200 次，旧组字非空 300 次、空 100 次。独立 edit-session 持有不可变来源 token、规范 composition 身份与结束回调内固定重力的私有范围；旧 `a` 只到旧 token，新 `b` 保持活动并在自己的结束后交付一次。
- 5,200 个 session、1,900 个结束记录与 1,900 个 range 包装对象全部析构；每轮已知对象归零，重复交付、固定范围失败与回调失败均为零。成功依据实际析构计数，5 秒仅是失败期限；不证明 TSF 内部内存全部释放。
- TSF 另产生 1,100 对隐式 Start/End，其中 700 对发生在清理阶段；把所有结束都当提交会删掉新 `b`。实验依赖合成生产者的显式身份标记，隐式记录只释放、不读写；不能照搬该标记，真实拼音对照见下。
- 公开 owner 对照：1,900 次 Start 与 1,900 次 End 的 [`GetOwnerClsid`](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcompositionview-getownerclsid) 全部成功，`GetClientId` 将 700 对清理期隐式组字映射到宿主 client，其余 400 对隐式组字仍属合成生产者。显式应用组字也映射到宿主，故 owner 只能区分本实验的 client，不能单独判定隐式组字或输入期；零 null 样本不代表可以忽略 null。
- 无效 flags 请求返回 `E_INVALIDARG`，记录未执行即析构；这是参数拒绝，不是运行时锁拒绝。排队返回 `TF_S_ASYNC` 后 `Pop` 也使记录未执行即析构；证明取消释放，不证明提交保留。

## 真实拼音：延后应用清理（2026-11-09）

- 可见独立宿主回读活动档案后按 A／Return，输入法 Start/End owner `{D7487DBF-804E-41C5-894D-AD96FD4EEA13}` 映射 client 2；清理 owner `{626761AD-78D2-44D2-BE8B-752CF122ACEC}` 映射宿主 client 32，查询全部 `S_OK`。owner 不等于档案 service CLSID，不能按两者相等过滤。
- 另一探针保留结束时的规范 identity 与私有 clone，同步 READ 固定双端重力后，人工暂缓清理；再按 A 启动新组字，然后异步清旧范围。旧范围为 `a`，全文 `aa → a`，新 composition identity 不变、范围与预编辑仍为 `a`。第二次 Return 后再清新范围，全文归零；两个独立记录均执行并释放，零 `unsupported`。初轮同结果；发现临时增量构建漏编头文件日志后，干净重建复测全部捕获、固定与清理 HRESULT 均成功。
- 清理引发的宿主 owner 回调没有替换活动输入法 composition。本实验只按公开 owner→client 区分本轮宿主清理，不按输入法名字分支；日语与小狼毫本轮只产普通字符，没有组字样本，全程未读或切模式，试后恢复初始档案。
- 这是人工延后应用清理，不是输入法自然迟到；没有产品提交回调或页面写回，也未验证真实输入期绑定、自然快速重新聚焦和未知 owner。保留独立请求记录作为下一原型方向；普通字符来源仍独立未解，尚未选定产品架构。

## 档案继承

- 三进程：A 进程范围激活拼音、US、日语后新建的 C 初始仍为小狼毫，显式选择才匹配；切 US 时已有 B 仍为拼音。只反驳无焦点时的自动继承，不证明获焦后的档案或模式。

## 剩余实施门槛

- 0057 不选择存储后端或线程划分，也不要求恢复旧结果归属调查。真实失焦、用户模式保持、旧送键撤销、授权重建及资源回收仍未完成，有限批次通过不能代替完整链路；本次仅更新文档，不恢复实验。
