---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 候选界面通过补丁 StreamingAssets 实现，不使用 CDP 注入

键盘页 WebView 关闭了远程调试，外部无法注入脚本，因此候选栏只能通过修改 StreamingAssets 里的键盘页资源实现。补丁用锚点匹配而非行号，并保留原文件哈希、备份与还原路径。

## Considered Options

- CDP 注入：远程调试已关闭。
- 自建 overlay 渲染候选：可行，但候选就不在键盘上了，与需求不符；保留为补丁失效时的兜底。
- 补丁键盘页：采纳。

## Consequences

- Steam 更新会整体覆盖该目录，补丁必须可校验、可重打、可还原。
- 候选栏必须随布局重建流程一起创建，否则切换布局后消失。
