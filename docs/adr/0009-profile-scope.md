---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 输入法的枚举与切换只作用于插件自己的线程

需求要求像 Windows 那样选择输入法，但插件只能操作自己的线程管理器。因此输入法列表来自 ITfInputProcessorProfiles 的枚举结果，切换用 ActivateLanguageProfile；两者都只作用于调用线程，不改动系统默认输入法。

同功能的 ITfInputProcessorProfileMgr 在本进程 QI 返回 E_NOINTERFACE，用不了，因此改用老接口。实测老接口能列出输入法与描述。

## Consequences

- 界面显示的"当前输入法"只代表组字线程的状态，可能与用户在系统里看到的默认输入法不同。
- 手段受限于本进程能否 QI 到 ProfileMgr。若将来可用，可换成它的批量查询与标志位，本决策的意图不变。
