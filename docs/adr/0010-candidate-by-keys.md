---
status: accepted
amended_by: ['0018']
---

# 候选只能靠喂按键选中

ITfCandidateListUIElement 是只读的，小狼毫实现的 ITfCandidateListUIElementBehavior 也只改高亮或关闭候选窗，没有"选中第 N 个并提交"的接口。因此点选候选的做法是向组字线程喂键：默认走 VK_DOWN 若干次加 VK_RETURN，检测到 Rime 时改用页内数字键；翻页喂 VK_PRIOR 与 VK_NEXT，并且不显示页码。

## Considered Options

- 依赖 ITfCandidateListUIElementBehavior：语义不足，无法提交。
- 喂按键：采纳，且对所有 TSF 输入法都成立。

## Consequences

- 翻页与选中行为取决于输入法自身的按键配置，需要按输入法适配。
- 页码不显示，因为小狼毫报告的页码信息不真实。
