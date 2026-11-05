---
status: accepted
supersedes: []
superseded_by: []
amends: ["0006"]
amended_by: []
---

# 用 TSF 隔间强制打开输入法并在结束时还原

宿主要组字，前提是线程的输入法处于打开状态；本机输入法默认是英文，不打开就只会落下明文。合成 Shift 键取决于输入法的键位配置（本机小狼毫把 `Shift_L` 绑成 `commit_code`，不是开关），无法可靠切换。因此改为直接写线程隔间 `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE`：读取原值、置 1，结束时写回原值。

## Considered Options

- 合成 Shift 键切换：依赖输入法键位配置，实测不可靠。
- 用 [0009](./0009-profile-scope.md) 的 `ActivateLanguageProfile`：那是选择输入法配置，不是开闭状态。
- 要求用户自己把输入法切到中文：把验证的成败交给用户状态，自检无法稳定复现。
- 写 `GUID_COMPARTMENT_KEYBOARD_OPENCLOSE` 并还原：采纳。

## Consequences

- 强制打开会改变整条线程的输入法状态，属于"抢占"，按 [0006](./0006-composition-host.md) 的规矩必须在结束时还原；还原失败只记录日志，不阻断收尾。
- 隔间的值读不出来时日志写 `unreadable`，不折叠成 0，避免把读失败当成"已关闭"。
- 取客户端标识的 `ITfThreadMgr::Activate` 失败即中止，不再拿 0 当客户端标识继续调用。
- 释放时不调用 `ITfThreadMgr::Deactivate`：实测加上它，自检结束的瞬间整个游戏进程退出。这一条与常见的成对写法相反，是实测结论，不是疏忽。
