---
status: accepted
amends: ['0055']
---

# 前台空闲检查排除 NLS 模式键

Windows NLS 可以在触发键松开后保留 `0xF0/0xF1/0xF2` 的异步 down 状态，导致插件误判按键未释放，见[根因调查](../archived/notes/first-open-foreground-2026-11-10.md)。修订 [0055](./0055-use-one-alt-pair-after-a-denied-focus-request.md)：所有键盘输入项统一从 Alt 回退的空闲检查中排除这三个模式码，保留其余键、鼠标按钮、活动菜单和资格检查，不查询输入法模式或增加重试。

这些编码同时代表 `OEM_ATTN/FINISH/COPY`，现有接口不能区分来源，用户接受同码特殊键不再阻止 Alt 回退。仅排除已确认的三个模式码，避免扩大到其余 DBE 编码；保留同码特殊键保护则需要另一种输入来源观测机制。
