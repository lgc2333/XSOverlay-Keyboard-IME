---
status: superseded
superseded_by: ['0057']
---

# 按输入目标保留原生文档

同一输入目标失焦再聚焦时，保留它的原生文档；[0050](./0050-hold-the-host-focus-for-the-composer.md) 的前台租约与送键资格独立结束，旧排队键不得因文档保留而恢复资格。真实拼音已在 A→B→A 后向 A 交付旧结果，逐聚焦期创建文档则尚无可靠的任意迟到结果回收边界，见[证据](../handoffs/archived-notes/context-isolation.md#真实-transitory-输入2026-11-09)。

结果归原输入目标，并在到达时写入原 composer 输入框的当前选区，不恢复旧插入位置或重新聚焦。隐藏与普通失焦不销毁目标；页面真正替换、导航到新文档或销毁时结束旧目标，旧结果不得写入替代目标。布局重建只有确认仍属同一输入目标时才延续文档，不能只凭相同 DOM ID 认领。

本决定确定寿命边界，不选择 TSF 存储后端或线程数量。目标身份、重建关联、旧送键撤销和实际销毁收尾仍须验证，当前证据与缺口见[上下文隔离](../handoffs/archived-notes/context-isolation.md)。
