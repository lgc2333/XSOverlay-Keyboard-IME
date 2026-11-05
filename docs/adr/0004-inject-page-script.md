---
status: accepted
supersedes: []
superseded_by: []
amends: []
amended_by: []
---

# 候选栏由插件在运行时注入键盘页

键盘页 WebView 关闭了远程调试，外部无法注入脚本；但插件运行在游戏进程内，直接持有键盘 overlay 的 `IWebView`，可以用 `ExecuteJavaScript` 把打包好的脚本送进页面。因此候选栏与写回都做成一个 TypeScript bundle，在接管第一次用到键盘页时注入，不改动游戏目录里的任何文件。

本 ADR 早先的结论是补丁 StreamingAssets，理由是"外部无法注入"。该理由对进程内插件不成立：`IWebView` 同时提供 `ExecuteJavaScript` 与 `PostMessage`，页面也能用 `window.vuplex.postMessage` 回消息。

## Considered Options

- 补丁 StreamingAssets：可行，但 Steam 更新会整体覆盖，补丁必须自带备份、哈希与还原。既然每次启动都能注入，就不必维护一份会被覆盖的副本。
- CDP 注入：远程调试已关闭，外部不可用；进程内也不需要它。
- 自建 overlay 渲染候选：候选就不在键盘上了，与需求不符。

## Consequences

- 候选栏仍然只在键盘页里，不在键盘之外另起渲染。
- 键盘页每次重建容器都会清掉注入的节点，注入脚本必须自己把节点补回去。
- 注入的 bundle 是构建产物：改前端要重新打包，仓库不保存打包结果。
- 页面到插件的消息走 `window.vuplex.postMessage`，插件用 `IWebView.MessageEmitted` 接收，属于事件驱动，见 [0005](./0005-event-driven.md)。
