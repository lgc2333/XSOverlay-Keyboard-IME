---
status: accepted
amends: ['0042', '0046']
---

# JIS 物理布局不显示中英切换按钮

[0012](./0012-synthesized-modifier-key.md) 明确要求 JIS 不插入中英切换按钮，该限制在后续语言栏按钮与布局实现中遗漏。恢复此边界：键盘页采用 JIS [物理布局](../../CONTEXT.md#布局)时始终隐藏按钮，不扣减空格宽度；ANSI 与 ISO 继续遵循 [0042](./0042-hide-the-switch-without-an-icon.md) 的显示条件与 [0046](./0046-place-the-switch-beside-left-alt.md) 的几何规则。

依据是键盘页实际采用的物理布局，不是当前输入法的语言；日语输入项配 ANSI 或 ISO 时仍按原条件显示，中文输入项配 JIS 时也隐藏。布局重建后仍应用同一规则。

否决整体废除 [0028](./0028-mirror-the-ime-language-bar-item.md) 等后续决定，它们的语言栏图标与原生点击机制仍需保留。本决定只修订按钮在 JIS 上的可见性，不恢复旧的模式切换实现。
