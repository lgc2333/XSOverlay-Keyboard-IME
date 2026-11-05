---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: ['0014', '0015']
---

# 由插件托管 TSF 组字上下文

系统输入法的组字只属于发起组字的线程，无法观察其他进程；而 XSOverlay 的输入框由 Vuplex 直接投递键事件，根本没有系统输入法参与。因此插件自建 STA 线程与隐藏 RichEdit 宿主，用全局 SendInput 把按键喂给当前输入法；候选从 ITfUIElementMgr 读取，组字串与提交文本则在组字事件回调里从宿主窗口文本读取。

## Considered Options

- 自己实现 ITextStoreACP：掌控力最强，但需要实现二十多个方法、上千行代码。
- 在 RichEdit 的 context 上挂 ITfTextEditSink 与 composition sink 取文本。否决：实测在组字活跃、候选已出现（读到 6 个候选）时，该 context 对两个 sink 均返回 0x80040202（TS_E_NOOBJECT）；判别实验确认回调对象能正确自 QI、对线程管理器挂 sink 也成功，因此是 context 不接受这两个 sink。
- 用 ITfKeystrokeMgr 的 KeyDown 把按键喂给输入法。否决：实测组字进行中同样返回 eaten=false，候选不更新。
- 定时比较窗口文本取提交。否决：定时比较属于轮询，违反 [0005](./0005-event-driven.md)。
- RichEdit 充当文本存储 + ITfUIElementSink 事件 + 事件时刻读窗口文本：采纳。

## Consequences

- 喂键只能走全局 SendInput，并且每次按键前都要断言宿主窗口在前台；抢占前台的行为必须记录并在结束时恢复。
- 组字串能否读到取决于输入法自己的行内预编辑设置。实测小狼毫：inline_preedit 关闭时窗口文本在组字期间时有时无（一次读到 "ni"，一次只读到空格），ImmGetCompositionStringW 返回空或垃圾；inline_preedit 开启时组字串稳定可读（窗口文本与 GCS_COMPSTR 都返回 "ni hao"），提交后窗口文本为 "你好"、GCS_RESULTSTR 同样返回 "你好"。
- 因此插件必须检测该设置：组字中读不到组字串时提示用户开启输入法的行内预编辑，并把组字串显示降级为只显示候选。
- 组字串与提交文本优先读 IMM：组字期间取 GCS_COMPSTR，EndUIElement 之后取 GCS_RESULTSTR。这样不需要改动控件、不需要维护文本基线，也不会累积。
- IMM 读不到时退化为窗口文本：组字期间读控件文本，提交后读完整控件文本与前一次基线的差值，再清空控件。只有当输入法完全不提供 IMM 数据时才会走到这条路。
- 候选通过 ITfUIElementMgr 读取，已验证能拿到数量、选中项与字符串。
- 该路线的前提是从线程管理器能取到宿主的文档管理器，已实测成立。
