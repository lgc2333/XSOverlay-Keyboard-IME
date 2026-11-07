# 兜底与特例排查（2026-11-07）

本轮只做只读排查，没有改任何代码。它接续 [26-11-07-1](./26-11-07-1_ownership-and-page.md)，但那一份的第 13 行记录着「切输入法前单独送一次 Enter，隔 60 毫秒再切」，该做法已被 `54f6141` 与 `c00cfe1` 全部撤回。两份冲突时以本文件为准。

## 一句话

主干是干净的：代码里没有按输入法名字的特例，喂键之后不再停顿，也不读中英模式。越界集中在三处——前台租约那一套（合成 Alt 加 5 次 40 毫秒重试）、启动时 8 秒盲等、语言栏按钮的静默退化；另有三处实现错误。文档里还留着一条会被后人当成规格重新实现的旧规则，见「文档」第 1 条。

## 固定点

排查与全部行号基于 `8358ffe`（`docs(roadmap): update ROADMAP.md`）。行号会随提交漂移，对不上时按内容搜。写这份文件时工作区另有一处未提交的 `README.md` 改动，不属于本文件的范围。

这次排查由四条独立的只读结论交叉核对：本次会话的直接阅读、两个独立子代理、一个文档考古子代理。它们对同一固定点各自取证，分歧处已由本会话读代码裁定，下文只写裁定后的结果。

## 已撤回的做法（不要重新加回来）

这四件事都曾实现过，都已被撤回。它们看起来像「补一个缺口」，实际是替输入法做它自己会做的决定。

- 合成选候选键（`VK_DOWN`、`VK_RETURN`、`VK_PRIOR`、`VK_NEXT`，并检测是否 Rime）：[0018](../adr/0018-candidate-bar-display-only.md) 放弃。
- 在布局里合成中英键：被 [0028](../adr/0028-mirror-the-ime-language-bar-item.md) 的镜像语言栏项取代。
- 写 TSF 隔间强制打开输入法：[0015](../adr/0015-ime-open-compartment.md) 被 [0025](../adr/0025-respect-ime-mode.md) 取代，后者又被 [0026](../adr/0026-faithful-callbacks.md) 取代。
- 切输入法之前先送 Enter 上屏、隔 60 毫秒再切、期间不还前台：`54f6141` 撤语言栏那一条路径，`c00cfe1` 撤 Win+空格 与下拉框，两次合计删除约 216 行（延迟切换、间隙定时器、前台持有、被扣住的键重放）。

## 待修 · 实现错误

这三条是代码错误，与设计取向无关，任何情况下都该修。

- **右手修饰键落成左手。** [KeyStrokeReader.cs:182](../../XSOverlayKeyboardIme/Takeover/KeyStrokeReader.cs#L182) 用 `IndexOf("RIGHT")` 判左右，而键盘页用的是 `LSHIFT`/`RSHIFT`（同文件第 13 行的注释自己写了这个命名）。`RSHIFT` 不含 `RIGHT`，所以 RightShift 与 RightControl 永远落成左边的键；测试样本只有 `LSHIFT`。按 `R` 前缀或 `E05B`/`E05C` 那种写法识别，并补 `RSHIFT`/`RCONTROL` 的测试。
- **粘滞记录先于发送改变。** [KeyFeeder.cs:84](../../XSOverlayKeyboardIme/Ime/KeyFeeder.cs#L84) 的 `ToggleModifier` 先改 `_latched` 再 `SendBatch`，`ReleaseLatchedModifiers` 先清空再发送。发送失败后，这份记录不再是「真的按住」的事实。改成发送成功后再改记录，或失败时回滚。
- **修饰键参与「组字中」判断。** [ImeHost.cs:98](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L98) 的 `inFlight` 用 `_strokes.IsEmpty` 判组字中，而修饰键也进这条队列，却在 [ImeHost.cs:493](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L493) 附近被直接发送、不开会话。于是粘住 Shift 后立刻按 Backspace，会被当成组字中喂给输入法。闸门要留（[0026](../adr/0026-faithful-callbacks.md) 记过快速连打漏提交），但修饰键不该进这条队列。

另有两处旧问题不算这次新发现：喂键的返回值在 [CompositionSession.cs:88](../../XSOverlayKeyboardIme/Ime/CompositionSession.cs#L88) 与 [ImeHost.cs:493](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L493) 附近被忽略，抢前台失败也只记日志就继续发；[KeyboardPage.cs:328](../../XSOverlayKeyboardIme/Page/KeyboardPage.cs#L328) 把任何未识别的页面消息都当成「页面已就绪」。

## 待修 · 兜底

这四条都没有说出它们在盖哪个失败。按 `AGENTS.md` 的「放行规则」，说不出名字的兜底就是缺陷本身。

- **启动时盲等 8 秒。** [ImeHost.cs:20](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L20) 与 [:202](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L202)：`CoInitializeEx` 之后睡 8 秒才建窗口与输入法上下文，期间消息泵不存在。没有任何 ADR 记录这 8 秒的必要性。改成等一个明确信号（页面就绪或第一次按键）再建窗，并把理由写成 ADR。
- **拿不到定时器就当成已稳定。** [ImeHost.cs:353](../../XSOverlayKeyboardIme/Ime/ImeHost.cs#L353)：`SetTimer` 失败就立刻 `Post(SwitchSettledMessage)`，跳过等待直接重读。这正是 [0036](../adr/0036-one-table-for-key-ownership.md) 说会「读到正在离开的输入法」的情形，等于把已知故障重新引入。注释里「否则 hold 永远不会释放」也已失效——前台持有在 `c00cfe1` 已删除。没有定时器就不重读，或记一条日志后放弃。
- **语言栏按钮的静默退化。** [LanguageBarButton.cs:171](../../XSOverlayKeyboardIme/Ime/LanguageBarButton.cs#L171)：挑不到「能画出图标」的语言栏项时退回第一个按钮，随后 [ImeHost.cs](../../XSOverlayKeyboardIme/Ime/ImeHost.cs) 会真的点击它，而且这条路径不打日志。返回 null 即可，页面已有「中/英」字形兜底，[0028](../adr/0028-mirror-the-ime-language-bar-item.md) 已接受这个退化。
- **候选栏的 2 秒 busy 超时。** [candidate-bar.ts:17](../../web/src/candidate-bar.ts#L17) 与 [:111](../../web/src/candidate-bar.ts#L111)：选了输入法后给 chip 挂 busy 态，2 秒没等到回报就自己清掉，等于宣称请求已完成。保留 busy 直到下一次状态推送。

## 待修 · 文档

1. **[0016:17](../adr/0016-key-interception-point.md) 是最高风险的一条。** 它仍以 `accepted` 身份写着「组字中按下提交键后输入法仍未提交时，宿主把组字串当作提交文本写回」，与 [0026](../adr/0026-faithful-callbacks.md) 的「不替输入法兜底」正面冲突，而代码里没有任何路径实现它（`CompositionSession` 只有 IME 回调与直通字符两条路），并且 0016 的 front-matter 没有 `amended_by`。后来者照着这条 ADR 写代码，会把刚撤掉的兜底原样装回来。处置二选一：在 front-matter 记 `amended_by` 并在正文加一行「不再适用」（[0018](../adr/0018-candidate-bar-display-only.md) 对 0010 就是这么做的），或由用户决定删除该段。
2. **[0036:21](../adr/0036-one-table-for-key-ownership.md) 的规则 5** 说「不在组字时，Sticky 的 Shift 与 Toggle 的 Caps 归页面」，而 [KeyStrokeReader.cs:32](../../XSOverlayKeyboardIme/Takeover/KeyStrokeReader.cs#L32) 的 latching 分支在 gate 判断之前，空闲态也返回「归输入法」。[0038](../adr/0038-latched-modifiers-follow-the-host.md) amend 了 0036，却没有写这条变了。
3. **[CONTEXT.md:50](../../CONTEXT.md#L50)** 仍说「粘滞与锁定由页面记着，插件不碰」，与 0038 的宿主按住相反。
4. **[CONTEXT.md:83](../../CONTEXT.md#L83) 与 [:87](../../CONTEXT.md#L87)** 把组字会话定义成「到提交键为止」、把空格与回车定义为提交键，与 [0033](../adr/0033-end-the-session-with-the-composition.md) 相反。
5. **0038 与 [0039](../adr/0039-switch-imes-from-the-bar.md) 的 `status` 仍是 `proposed`**，两者都已实现（`5b211ee`）。
6. **README 的四个翻译版仍停在旧事实**：`README.en.md`、`README.ja.md`、`README.ko.md`、`README.zh-TW.md` 的第 78 与 80 行还写着「按 Win+空格 不会改变 VR 键盘的输入法」「下拉框还没做」。第一句现在正好是反的。`README.md` 已更新。
7. **[26-11-07-1:13](./26-11-07-1_ownership-and-page.md)** 描述的「修饰键留页面、先送 Enter、隔 60 毫秒」已被推翻。交接文档是归档历史，不必改写，但读它时要带着 `54f6141` 与 `c00cfe1` 两个撤回提交。

已经修掉、不必再报的：0039 的「先上屏再切」、`ROADMAP.md` 第 16 行的 Shift 归属、0038 的文件名、Win+空格 里无条件抬 LeftWin。它们由 `53db6a7` 与 `5da8c60` 处理。

## 规则落点

这条原则已经写进 [AGENTS.md](../../AGENTS.md) 的「放行规则」一节，六条：输入法说了算；先按来源给缺陷分类（插件、XSOverlay、输入法、系统）；输入法或系统产生的结果是预期行为；只改插件自己造成的缺陷；兜底必须点名它盖的那个失败；等待、重试、定时器、合成按键要先证明直路走不通，并把证明写进对应的 ADR。

用词按 [CONTEXT.md](../../CONTEXT.md) 的词汇表：正式说法是「放行」，「透传」「直通」是避免词。

## 未定

- 8 秒盲等是否仍必要。它与 `26-11-05-2` 里自检的 8 秒数值相同，但没有文本或提交把二者连起来，不能断定同源。
- 合成 Alt 在 VR 会话里是否仍必要。唯一的书面记录是 `26-11-05-2` 第 30 行，说它在桌面模式对 `Ghost` 窗口无效；没有 ADR，也没有 VR 下的结论。
- 5 次 40 毫秒的重试是否真能救回来。日志只记最终结果，不记中间失败。
- 400 毫秒是实测值还是估值。ADR 只说「不等待会读到正在离开的输入法」，没给测量。
- 语言栏退化时实际会点到什么，取决于各输入法注册了哪些项，本机未验证。

## 需要用户拍板

1. 要不要立一条新 ADR，内容就是「输入法没提交，插件不替它提交」，用来正式废掉 0016:17？现在 0016、0036 规则 5、CONTEXT.md 三处都还在把人带回已否决的假设。
2. 400 毫秒的切换定时器删不删？删掉后按 Win+空格 切完，名字与图标会停在旧值，直到下一次按键或下一次语言变化。定时器里「重读组字状态」的那一半读的是缓存（`ImeContext` 的 `Composing` 字段），所以它对组字没有意义，只对名字与图标是真重读。
3. 空闲态的 Sticky 修饰键归谁？改代码回 0036 规则 5，还是改 0036 承认空闲也 latch。
4. 「实现错误」与「兜底」里的七项，要不要现在一次改完？按仓库的提交节奏，同一请求的改动攒成一个提交，文档跟着它所描述的代码一起走。
