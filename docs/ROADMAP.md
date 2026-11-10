# 路线图

## 已实现，待真机验收

- [ ] 首开输入法下拉框隐藏：空闲检查排除三个 NLS 模式码，见 [0063](./adr/0063-exclude-nls-mode-keys-from-the-foreground-idle-check.md)。真机保持 `0xF2=8000`，首次原生指针点击已取得前台并显示列表，关闭后归还前台，再打开首次点击成功。根因与验收边界见[前台失败追查](./archived/notes/first-open-foreground-2026-11-10.md)。
- [ ] 组字栏顶部固定预留 34px，两侧避开原生圆角，显示和隐藏不移动布局，见 [0061](./adr/0061-reserve-the-full-preedit-tab-height.md)。安装版素材截图已确认裁切与接缝修复，待确认 VR 缩放和观感。
- [ ] 模式按钮接入公开 TSF 优先、Shell 原图与任务栏 UIA 点击，见 [0051](./adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)。生命周期回归已通过，待微软拼音真实点击验收。
- [ ] Shell 图像订阅沿焦点存续，迟到首图与旧焦点回调已有回归覆盖，待确认真实输入法切换后的图标更新。
- [ ] ChatBox 正式发送成功后关闭本轮 typing，明确重新聚焦才恢复，失败保留广播，见 [0062](./adr/0062-end-chatbox-editing-after-successful-send.md)。安装版页面模块回归覆盖成功与失焦间隙，待 VRChat 实测。

## XSOverlay 侧问题，不由本插件修复

- JIS 平／片假与変換的扫描码字符串 `70/79` 未转换成功后被保留，XSOverlay 送键时又按十进制虚拟键码解释为 `F/O`。安装布局、游戏解析与真实页面事件已闭合；该部分仅记录，不修复，也不继续调查修复方案。此前用户观察的 `f/a` 目标为 VSCode，其中 `a` 的完整链路仍未确认，见[XSOverlay 侧缺陷](./archived/notes/upstream-xs-overlay.md)。
- 输入框失焦时，Alt+Shift+F 在 VSCode 中只打出大写 F。最后三次对照确认 VR 粘滞和保持都被识别为 AltRight / AltGraph，Z 丢失 Alt 修饰；物理 Alt+Z 正常。该段走 XSOverlay 原始模拟器，左 Alt 被错误标为扩展键；另报的 Alt 状态反转尚未再次复现，见[XSOverlay 侧缺陷](./archived/notes/upstream-xs-overlay.md)。注：在 commit 7a19e75 时的真实 VR 测试中未能复现，按键正常触发格式化，玄学问题。
