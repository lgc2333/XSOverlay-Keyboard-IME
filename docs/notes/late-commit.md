# 迟到提交、普通字符来源与失焦写回

提交无条件送达见 [0032](../adr/0032-deliver-a-late-commit.md)；写回用 `insertText` 并保留原生撤销见 [0008](../adr/0008-inserttext-writeback.md)；失焦交给输入法默认行为见 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md)。这里记来源身份与写回边界的证据。

## 约束

- 用户不接受用 `setRangeText` 换取失焦写回；只有证明保留原生撤销不可行，才重新讨论统一丢弃失焦预编辑。真实 Vuplex 已证明存在保留撤销的直路，不采纳该退路。
- 写回绑定原页面、原 composer 输入框与原输入期；临时 DOM 选择不得重新授予送键资格。
- 一次提交不保证是独立撤销单元，撤销可能合并先前键入。

## 页面侧

- 隔离回归：组字后真正失焦，`insertText` 返回 `focus-lost`，页面随后请求取消。手动注入旧回调会替换快速重新聚焦后的新预编辑，也会写入重建的同名输入框：接口缺少来源身份，但不是真实 TSF 时序。
- 独立 Chrome（关闭焦点模拟）后台写回与撤销成功：保留 composer 为 activeElement 时 `p → pa → p → pa`，焦点在按钮也能写；只设选区的写法在另一个 input 持焦时写错框。临时选 composer、写入、恢复原元素的对照成功，但没经过生产观察器。
- headless Chromium 12 项同步对照：写回 `pa`、另一框保持 `q`；页面活动时观察器同任务变 true、下一任务变 false；延后一个任务才恢复的阳性控制能收到通知。异步写回不能套用同步结果。
- 预编辑仍活动时裸写会触发保护并请求取消；须按原输入期结束预编辑再写回。人工 window blur 不会让 `document.hasFocus()` 变 false。
- 真实 Vuplex：写入 `p` → `KeyboardFocus.Release`（scope `True/True → False/False`）→ 文档失焦后后台 `insertText/undo/redo` 得 `pa → p → pa`，activeElement 回 body，游戏 scope 均无焦点，未调用 `SetFocused(true)`/`SendKey`。前台 HWND 只比较命令首尾。
- 真实释放先 `SetFocused(false)` 再通知页面 `.blur()`，跨 IPC 无静态顺序；`SendKey` 会 `SetFocused(true)`，不能用于验证。Vuplex 诊断已记录过 `focusout → page-blur → composer-inactive` 且 `documentFocused=false`，说明能进背景 document 状态。

## 原生侧来源身份

- 生产 `TfCompositionHandler.cpp` 不保留开始时的 composition 身份：结束后排队同一 edit-session，执行时读取并清空整个持久上下文；回调只有字符串，写回时重新找当前页面。给回调发生时的当前输入期编号不能恢复来源。
- 合成对照以规范 `IUnknown` 身份匹配结束回调；按 `ITfClientId` 分离客户身份后，应用可读写固定的旧范围（全文 `ab`，只清旧 `a`），新组字保持 `b`；共用身份时改写旧范围返回 `TF_E_RANGE_NOT_COVERED`。
- 范围不是不可变快照：不固定重力会延伸到新文字；须用生产者持有的有效 write cookie 固定边界，结束回调里用创建上下文的 cookie 得 `TF_E_NOLOCK`。
- 匹配的结束回调内 `GetRange` 成功，结束返回后为 `E_UNEXPECTED`，不能等异步读取再取。
- 跨两个编辑会话时 `OnEndEdit` 的只读 cookie 可固定旧范围；但同一次编辑内先结束旧组字再启动新组字时，等 `OnEndEdit` 已太迟，清除会误删新文字。
- 结束回调内请求 `TF_ES_SYNC | TF_ES_READ` 固定私有克隆，四种合成对照都只清旧文字；`RequestEditSession` 对同步请求有条件限制。
- 真实日语：结束回调内 `GetRange`、克隆、同步请求、读取、重力全部 `S_OK`，原路径提交一个 `a`；另一次把克隆存进 COM edit-session 后排异步只读观察，`TF_S_ASYNC` 后仍读出 `a`，但该观察排在原清理之前、改变了调度。
- 临时宿主的代理对缓冲检查通过（前缀先交付、单高代理保留、成对才交付），只验证临时代码。
- 普通字符无来源字段：`TranslateMessage` 投递到线程队列，`WM_CHAR` 不保证与按键一一对应；注入按键的 `ExtraInfo` 与时间都不传到字符，不能反查输入期。
- 窗口身份也不能替代输入期：窗口过程忽略消息 HWND、统一写静态当前窗口，上下文绑定固定 HWND；显式投给旧窗口的字符在目标变新后仍到旧 HWND。重定向 HWND 再提前排空字符的候选不采用：契约不保证与真实键盘、死键或 IME 路径等价。

## 原生依赖的 COM 缺陷

- 上游 `COM_DEF_BEGIN` 查询 `IUnknown` 时返回完整对象指针，但首基类是含虚析构的 `ComObjectBase`，真正的 COM 接口在另一地址；同结构 MSVC x64 对照中四个接口返回同一错误地址，首个接口偏移 16。未调用错误虚表触发崩溃。
- `InternalRelease` 在计数归零 `delete this` 后仍返回已释放对象内的 `ref`；AddressSanitizer 检出 4 字节 heap-use-after-free。
- 已按用户授权 fork 修复（提交 `a95d660`）：三个生产 handler 明确选择自己的标准 COM 接口身份，删除前保存剩余计数；C ABI、输入处理与 TSF 会话策略不变。新增 `ComObjectBaseTests` 覆盖多接口身份、经查询接口调用、失败查询、引用保活与恰好一次销毁；根构建与 CI 接入 CTest，子模块来源与许可证指向 fork 并保留上游基点。
- 修复库仍通过原有合成 TSF 隔离对照。尚未部署，也未证明这两处缺陷导致现场症状；它们不解决 raw owner 的异步存活期、候选归属或上下文回收。

## 待验证

- 真实迟到提交绑定原页面、原 composer 与原输入期；快速重新聚焦后旧提交不得写新目标。
- 物理 Ctrl+Z、另一个 input 持焦、页面重建与关闭。
- 跨输入期的普通字符来源，以及提交与字符来源贯穿 STA、Unity 队列与写回。
