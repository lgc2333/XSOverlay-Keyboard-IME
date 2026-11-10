---
status: accepted
amends: ['0052']
---

# 正式发送成功时结束本轮 ChatBox 编辑

正式发送成功后，原生页面清空正文与失焦之间的新快照会短暂重开 typing。本决定修订 [0052](./0052-broadcast-chatbox-previews-and-keep-native-submission.md)：成功即结束本轮编辑，直到用户再次明确聚焦才恢复广播；发送失败恢复此前广播，首次空输入框聚焦仍开启 typing。发送成功到失焦之间的输入沿用 XSOverlay 的结束行为，不取消失焦，也不根据正文是否为空判断成功。
