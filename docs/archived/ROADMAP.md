# 路线图

## XSOverlay 侧问题，不由本插件修复

- JIS 平／片假与変換的扫描码字符串 `70/79` 未转换成功后被保留，XSOverlay 送键时又按十进制虚拟键码解释为 `F/O`。安装布局、游戏解析与真实页面事件已闭合；该部分仅记录，不修复，也不继续调查修复方案。此前用户观察的 `f/a` 目标为 VSCode，其中 `a` 的完整链路仍未确认，见[XSOverlay 侧缺陷](./archived/notes/upstream-xs-overlay.md)。
- 输入框失焦时，Alt+Shift+F 在 VSCode 中只打出大写 F。最后三次对照确认 VR 粘滞和保持都被识别为 AltRight / AltGraph，Z 丢失 Alt 修饰；物理 Alt+Z 正常。该段走 XSOverlay 原始模拟器，左 Alt 被错误标为扩展键；另报的 Alt 状态反转尚未再次复现，见[XSOverlay 侧缺陷](./archived/notes/upstream-xs-overlay.md)。注：在 commit 7a19e75 时的真实 VR 测试中未能复现，按键正常触发格式化，玄学问题。
