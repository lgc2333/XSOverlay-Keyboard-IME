# 迟到提交、普通字符来源与失焦写回

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

现行结果规则见 [0057](../../adr/0057-apply-ime-results-to-the-current-composer.md)：按当前有效且聚焦的 composer 处理，否则丢弃，不追溯迟到结果。会话结束不单独阻止提交，见 [0032](../../adr/0032-deliver-a-late-commit.md)；写回保留 [0008](../../adr/0008-inserttext-writeback.md) 的原生撤销，失焦默认行为仍遵循 [0050](../../adr/0050-hold-the-host-focus-for-the-composer.md)。

## 约束

- 不用 `setRangeText` 替代原生撤销；结果交付与丢弃边界以 0057 为准，不再要求后台写回原目标。
- 0053 已被取代；不为迟到结果维持跨聚焦的目标身份或文档寿命。结果处理不能恢复已撤销的送键或前台资格。
- 提交写入当前 composer 的当前选区，不保存旧插入位置；接受重新聚焦后旧结果混入的限制。
- 一次提交不保证是独立撤销单元，撤销可能合并先前键入。

## 已确认的组字显示

显示方式见 [0056](../../adr/0056-show-preedit-in-a-composer-tab.md)，已取代临时写入正文的 0019。短拼音、长组字与日语的离线显示和撤下不改正文，原型总高固定 498px。十二个字首、字中、字尾对照发现，拆开拼音排版会让宽度变化 0.25px；整段文字与独立光标保持宽度及 padding 不变。长串内容区裁切、光标跟随和垂直居中也已通过，字素组合没有从中拆开；这是给定位置的浏览器几何对照，输入法位置接线、命中、宿主尺寸同步与真实 VR 效果仍须验收。

## 当前焦点丢弃缺口

- 未修改的 `KeyboardPage` 两组 source-link 对照、9 项检查确认：inactive 时 `Show` 仍缓存并提交组字，后续 `Attach` 重放；Hidden 状态也不清该缓存。Vuplex 为记录表达式的 mock，不证明实际页面执行。
- 完整未修改 bundle 的四页 Chromium 对照、19 项检查确认：另一 input 持焦、composer 缺失或合成 window blur 时，提交均为 `focus-lost`，正文不变，但两个候选仍渲染。另一 input 与合成 blur 两例在等待及 refocus 后仍保留候选；缺失 composer 后新建节点则由正常取消清除。聚焦阳性按当前选区原生插入一次，网络与页面错误均为零；合成 blur 不是真实后台。
- 上述差异来自插件缓存与显示入口，未使用 TSF 或输入法行为。0057 要求同时验证不缓存、不显示及不补发，不能只以提交被拒绝判定通过；真实宿主与 Vuplex 往返仍须验收。

## 页面侧

以下保留旧路线的实验事实；原目标归属、后台写回和跨焦点结果隔离已不作为实施门槛，既有阳性与反例不因 0057 失效。

- 隔离回归：组字后真正失焦，`insertText` 返回 `focus-lost`，页面随后请求取消。手动注入旧回调会替换快速重新聚焦后的新预编辑，也会写入重建的同名输入框：接口缺少来源身份，但不是真实 TSF 时序。
- 独立 Chrome（关闭焦点模拟）后台写回与撤销成功：保留 composer 为 activeElement 时 `p → pa → p → pa`，焦点在按钮也能写；只设选区的写法在另一个 input 持焦时写错框。临时选 composer、写入、恢复原元素的对照成功，但没经过生产观察器。
- headless Chromium 12 项同步对照：写回 `pa`、另一框保持 `q`；页面活动时观察器同任务变 true、下一任务变 false；延后一个任务才恢复的阳性控制能收到通知。异步写回不能套用同步结果。
- 预编辑仍活动时裸写会触发保护并请求取消；须按原输入期结束预编辑再写回。人工 window blur 不会让 `document.hasFocus()` 变 false。
- 真实 Vuplex：写入 `p` → `KeyboardFocus.Release`（scope `True/True → False/False`）→ 文档失焦后后台 `insertText/undo/redo` 得 `pa → p → pa`，activeElement 回 body，游戏 scope 均无焦点，未调用 `SetFocused(true)`/`SendKey`。前台 HWND 只比较命令首尾。
- 真实释放先 `SetFocused(false)` 再通知页面 `.blur()`，跨 IPC 无静态顺序；`SendKey` 会 `SetFocused(true)`，不能用于验证。Vuplex 诊断已记录过 `focusout → page-blur → composer-inactive` 且 `documentFocused=false`，说明能进背景 document 状态。
- 六页安装版源码对照发现，后台写回临时 `.focus()` 会触发原 focus listener 的 `RequestKeyboardInputFocus`；恢复 other 或 body 后，请求与 pending 仍在，身份回复迟到时也会继续发送。窗口 capture 的同步对照挡住该 listener，但不清除旧 pending。socket 为内存 mock、背景状态为合成，未证明真实 Vuplex 重新授权；写回还须隔离这条请求副作用，不能只比较首尾焦点。
- 完整生产 bundle 的内存候选先验六页、64 项：预编辑仅独立显示，旧结果替换当前 `[6,10,backward]` 后，新 `hao` 保持活动，取消与聚焦请求增量为零；真正移光标与外部改值仍各取消一次，撤下显示后原生 undo／redo 有效。候选只在同步内部写回期间跳过光标核对，成功后更新预期值和选区；其余观察器保留。节点替换拒绝，other 与旧 pending 保留；这些页面当时实际持焦。
- 同步条件对照发现，裸认领插入后的任意值会吞掉 input handler 的外部改值。另四页、33 项对照只在原节点仍有效、文字和选区均等于预期编辑时更新观察器：正常插入保留 `hao`，只读失败不改状态，条件外部改值或替换节点均拒绝认领并正常取消一次，嵌套写回拒绝。未回滚外部变化；原生命令已接受后仍可能报告失败，不能据此重放提交。只验给定 BMP 文字，未建立通用规范化校验，条件处理器也非已证实的安装路径。
- 完整 bundle 与上述精确认领候选再验真实后台：完整 Chromium 关闭焦点模拟，三例六页、144 项通过，源文档实际 `hasFocus:true→false`、另一页为 true。BODY、other 与活动 `hao` 三例均可按到达时当前选区插入，并保持后台完成有效 undo／redo；内部聚焦请求、active／inactive 与取消增量均为零，other 选区与旧 pending 保留。原 ChatBox 观察器正常报告 inactive 文本更新，未声称桥消息静默。`hao` 在自然失焦后已由原 60 毫秒路径取消一次，不是后台保留组字的证明。返回前台后显式 focus 仍正常报告。来源与输入期仍由实验给定；真实 Vuplex／TSF、活动组字重建及更早 capture 的副作用尚未闭合，显示方式已由 [0056](../../adr/0056-show-preedit-in-a-composer-tab.md) 确认，产品尚未实施。
- 直接打包当前生产脚本的 13 项离线对照有 6 个来源隔离反例：旧提交在 blur／refocus 后替换新预编辑，旧 idle 清除新预编辑，同 ID 替换接收旧提交或 idle，延迟执行的现有包装字符串写入新 document。旧 60 毫秒定时器确实跳过新组字；它的局部 epoch 没有覆盖宿主回调。对照人为安排次序，不证明真实 TSF 或 Vuplex 会这样调度，也没有决定丢弃真实迟到结果。
- 原后台撤销 fixture 拒绝替换节点、变化值的旧 cleanup 与新 document，但同一节点 blur／refocus 后仍接受写入；节点与值检查不能证明输入期隔离。
- 新 preedit 与旧提交交叉的 DOM 候选未通过撤销：给定原节点与锚点，暂撤 `hao`、原生插入 `OLD`、再显示并撤下 `hao`，显示可得 `BOLDhao → BOLD`，但 undo／redo 虽返回 true 并发出历史事件，值始终为 `BOLD`。Ctrl+Z／Y 同样失败；不重新显示 preedit 的隔离控制能正确 `BOLD → B → BOLD`。这是 Chromium 的合成 DOM 对照，未验证 Vuplex；不能把临时移除、插入、重画视为已保留撤销的方案，也没有决定取消新组字。
- 未修改的生产 bundle 也复现撤销空操作：`ni → 提交你 → idle → hao → idle` 后，undo／redo 都返回 true 并发出可信历史事件，值始终为 `B你`；光标观察器取消第二次组字也一样。单次提交与两次完整提交的最后一次撤销正常。仅在独立 span 显示预编辑的机制对照保留撤销，但原节点与锚点由实验给定；未验证真实 Vuplex，也未选用该显示方式。
- 版本差异已补对照：安装版 CEF 的文件版本含 Chromium `137.0.7151.104`；公开同分支 headless Chrome `137.0.7151.119` 上，六项生产 bundle 序列与原 `158.0.8127.18` 结果一致。上述两种撤销空操作均复现，单次和完整提交对照仍正常；不能再只归因新版 Chromium，但精确补丁、CEF 嵌入与真实 Vuplex 仍待实测。
- 独立单文件机制原型进一步保留新预编辑 `hao`，旧提交原生插入后仍能 `BOLD → B → BOLD`；另一个 input 的焦点与反向选区也能恢复。原节点替换时不写新节点。输入期、节点与初始锚点均由实验给定，历史操作后的锚点明确标为待确认；不代表来源、通用锚点维护或真实页面观察器已闭合。

## 原始插入位置候选（未采用）

以下保留有限调查证据，不作为当前写回语义的实施门槛。

- 八个原生历史对照中，`data=null`、`getTargetRanges()=[]`，但历史 `input` 能读到恢复后的选区；重复文本的相同值变化仍可由选区区分。恢复光标不能直接当编辑范围：`abc → 两次退格 → d` 的合并撤销把 `Bad` 恢复为 `Babc`、光标 4，实际源范围是 `[2,3]`。
- 若要求旧提交保留原位置，一条有限候选是记录实际编辑范围与锚点快照，再用值及恢复选区枚举原生撤销分组。六个新页面的 14 次编辑、19 次历史事件均唯一匹配，编辑前已存在的两个外侧锚点与独立文本位置一致，覆盖重复文本、同值替换、合并历史、分支与代理对替换，没有替换浏览器历史。
- 后建外侧锚点可在已知编辑范围上逆推快照：另六页的 15 次编辑、23 次历史事件中，22 次已知历史唯一匹配，12 个已接纳锚点在仍确认位置时均与独立参照一致，覆盖合并删除与输入、撤销后创建及分支后创建。未知旧历史的一次撤销无匹配，候选撤销位置确认；区内与两端边界也拒绝逆推。未知组与已知端点碰撞、边界归属、其他合并方式及真实 Vuplex 仍未闭合，拒绝定位本身不满足迟到提交的交付要求。

## 原生侧来源身份

- 原隔离方案曾区分三种来源：已捕获后传输迟到的快照、refocus 后才读取的保留文档组字、旧应用请求误改新组字；显示序号不能独自解决后两者。当时要求文字仍交付原目标，现已由 0057 取消，保留此记录解释旧候选的复杂度。
- transitory 回调在有效 cookie 下立即读取的 result 与 composition 分别是确定结果和当前组字；它们没有逐键或焦点期字段，可作为当时原文档的观察快照，不能认定来自同一次输入。可变 range 本身不是冻结快照。
- 生产 `TfCompositionHandler.cpp` 在 `OnEndComposition` 当场发 End，再排同一异步 edit-session；`DoEditSession` 执行时读取并删除整个当前上下文，再提交字符串，并不直接发 End。实际处理器、shim 与 source-linked 托管链的两个受控对照已确认：旧 `a` 请求等到新组字 `b`、全文 `ab` 后执行，会提交 `ab`、清空全文，并使 `ImeContext` 与 `CompositionSession` 发布 idle；同一新 composition 仍存活，范围已空。清理还产生隐式组字，其结束会清共享 view。实验人工截留异步请求、发布新 view 并设定 session-open 前提，未验证真实 TIP 自然时序或游戏；已确认的是插件机制在该时序下的后果。写回仍重新找当前页面，当前期编号不能补回来源。
- 合成对照以规范 `IUnknown` 身份匹配结束回调；按 `ITfClientId` 分离客户身份后，应用可读写固定的旧范围（全文 `ab`，只清旧 `a`），新组字保持 `b`；共用身份时改写旧范围返回 `TF_E_RANGE_NOT_COVERED`。
- 范围不是不可变快照：不固定重力会延伸到新文字；须用生产者持有的有效 write cookie 固定边界，结束回调里用创建上下文的 cookie 得 `TF_E_NOLOCK`。
- 匹配的结束回调内 `GetRange` 成功，结束返回后为 `E_UNEXPECTED`，不能等异步读取再取。
- 跨两个编辑会话时 `OnEndEdit` 的只读 cookie 可固定旧范围；但同一次编辑内先结束旧组字再启动新组字时，等 `OnEndEdit` 已太迟，清除会误删新文字。
- 结束回调内请求 `TF_ES_SYNC | TF_ES_READ` 固定私有克隆，四种合成对照都只清旧文字；`RequestEditSession` 对同步请求有条件限制。
- 真实日语：结束回调内 `GetRange`、克隆、同步请求、读取、重力全部 `S_OK`，原路径提交一个 `a`；另一次把克隆存进 COM edit-session 后排异步只读观察，`TF_S_ASYNC` 后仍读出 `a`，但该观察排在原清理之前、改变了调度。
- 单 context 有限压力实验已验证独立记录的隔离与析构，但暴露清理产生的隐式组字，不能将每个结束都当用户提交；数字与边界见[上下文隔离](context-isolation.md#有限请求回收实验2026-11-09)。
- 真实拼音已验证人工延后旧私有范围清理时，新组字保持不变；这不等于自然迟到或产品来源链验证，见[真实拼音对照](context-isolation.md#真实拼音延后应用清理2026-11-09)。
- 临时宿主的代理对缓冲检查通过（前缀先交付、单高代理保留、成对才交付），只验证临时代码。
- 普通字符无来源字段：`TranslateMessage` 投递到线程队列，`WM_CHAR` 不保证与按键一一对应；注入按键的 `ExtraInfo` 与时间都不传到字符，不能反查输入期。
- [普通翻译的字符消息](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-keyboard-input#character-messages) 在下一轮消息循环优先处理，支持继续验证隔离的同步翻译批次；它不是异步 TSF 的来源或完成屏障。`PeekMessage` 还会分派非排队消息，紧接翻译排空不能单独证明归属；死键跨期、多字符与代理对仍须覆盖。`WM_IME_CHAR`、`WM_UNICHAR` 也没有输入期字段，换消息类型不能补身份。
- 当前唯一普通字符收集入口是 `WM_CHAR`；原生 TSF 提交直接走字符串回调，并不投递该消息。直接编译未修改的生产窗口过程，在隐藏 message-only HWND 中送入 `WM_IME_CHAR`，默认处理确实排队生成 `WM_CHAR`；跨线程非排队通知能在精确字符过滤的同一次 `PeekMessage` 内产生它。未调用 `TranslateMessage`，因此紧邻翻译批次不是唯一来源；没有证明真实 TIP 会跨输入期发送该消息。该宿主的 `WM_UNICHAR` 返回 0，未生成字符，不能据文档泛推本机支持。
- 显式 TSF 存储也不保证排除兼容字符：微软 [tsfpad-step4](https://github.com/microsoft/Windows-classic-samples/blob/8f31b1ff79d669b4ba9609f2640635b3b8a9e0a4/Samples/Win7Samples/winui/input/tsf/tsfapps/tsfpad-step4/TextInputCtrl.cpp#L137) 同时保留 `WM_IME_CHAR → DefWindowProc → WM_CHAR`；[`ImmAssociateContext(NULL)`](https://learn.microsoft.com/en-us/windows/win32/api/imm/nf-imm-immassociatecontext) 的合同涉及禁用窗口输入法，不能当作无损排除。`ToUnicodeEx` 的同步结果尚未覆盖真实 Unicode packet、死键与 Alt release，不能替代标准翻译后宣称来源已闭合。
- 窗口身份也不能替代输入期：窗口过程忽略消息 HWND、统一写静态当前窗口，上下文绑定固定 HWND；显式投给旧窗口的字符在目标变新后仍到旧 HWND。重定向 HWND 再提前排空字符的候选不采用：契约不保证与真实键盘、死键或 IME 路径等价。
- 显式 ACP 文档加独立 HWND 已通过框架异步写入与字符隔离；它给出替代来源边界，但真实输入法的迟到目标和提交抽取仍须验证，见[文本存储对照](context-isolation.md#显式文本存储替代验证方向)。
- transitory sink 已通过原目标绑定、result-only 删除、null 范围与有限批次完整回收；它不依赖把每个 composition end 识别为提交。真实拼音 A→B→A 中，B 已提交后，旧 A 才产生仍归 A 的结果；日语普通字符立即归各自 HWND。这曾支持 0053，现仅保留为后端证据，不再要求按原目标交付，见[真实 transitory 输入](context-isolation.md#真实-transitory-输入2026-11-09)。

## 原生依赖的 COM 缺陷

- 上游 `COM_DEF_BEGIN` 查询 `IUnknown` 时返回完整对象指针，但首基类是含虚析构的 `ComObjectBase`，真正的 COM 接口在另一地址；同结构 MSVC x64 对照中四个接口返回同一错误地址，首个接口偏移 16。未调用错误虚表触发崩溃。
- `InternalRelease` 在计数归零 `delete this` 后仍返回已释放对象内的 `ref`；AddressSanitizer 检出 4 字节 heap-use-after-free。
- 已按用户授权 fork 修复（提交 `a95d660`）：三个生产 handler 明确选择自己的标准 COM 接口身份，删除前保存剩余计数；C ABI、输入处理与 TSF 会话策略不变。新增 `ComObjectBaseTests` 覆盖多接口身份、经查询接口调用、失败查询、引用保活与恰好一次销毁；根构建与 CI 接入 CTest，子模块来源与许可证指向 fork 并保留上游基点。
- 修复库仍通过原有合成 TSF 隔离对照。尚未部署，也未证明这两处缺陷导致现场症状；它们不解决 raw owner 的异步存活期、候选归属或上下文回收。

## 待验证

- 按 0057 验证当前有效聚焦时显示或写回、无焦点时丢弃且不补发；不再将迟到结果来源隔离列为门槛。
- 物理 Ctrl+Z、另一个 input 持焦、页面重建与关闭。
- 普通字符和提交完整且不重复交付；原生清理与资源释放仍须安全完成，旧结果不能重新授予送键资格。
