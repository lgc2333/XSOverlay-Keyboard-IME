---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 提交文本用 execCommand('insertText') 写回输入框

组字结果必须落进 Chromium 的输入框。逐字符投递键盘事件对中文无效，也不会产生正确的输入事件；而 execCommand('insertText') 在 Chromium 里同时维护撤销栈、光标与 beforeinput/input 事件。因此提交时对 document.activeElement 执行该命令，写入前先用同一通道确认焦点仍在目标输入框。

## Consequences

- 写入失败必须放弃本次提交并记录日志，不能退化成逐字符投递。
