# 路线图

## 当前待解决

原六项输入问题的来源、实施状态与验收见[输入修复笔记](./notes/README.md)。宿主随 composer 聚焦持有前台，失焦遵循输入法默认行为，见 [0050](./adr/0050-hold-the-host-focus-for-the-composer.md)；生命周期实现与验证尚未完成，见[宿主前台租约](./notes/host-lifecycle.md)。

物理编辑键的独立消息泵对照已验证真实拼音消费，但页面路由尚未连接；下一步优先验证显式过滤后的按键归属与原生编辑，见[宿主前台租约](./notes/host-lifecycle.md)。

- [ ] 微软中文与日语输入法在输入框为焦点时打不出任何字，不管是中文还是英文模式，打不出一个字，没有预编辑，没有候选，没有一个字上屏输入框。英文模式也如此。独立生产宿主对照已重复：原样归还前台时无输入，仅抑制归还后拼音开始组字、日语收到普通字符，支持插件过早归还前台；提权游戏内的干预尚未完成。日语迟到字符未立即交付也是插件缺陷，见[宿主前台租约](./notes/host-lifecycle.md)。
- [ ] 键盘在首次打开并聚焦输入框时，切换输入法无效。点击一次桌面浮窗中的其他窗口后恢复正常。真实 VR 对照已确认前两次原生调用直接返回 false、前台未变，桌面交互后原调用返回 true 并成功切换；系统拒绝的具体条件仍待确定，见[首次前台获取](./notes/foreground-acquire.md)。

## 待真机验证

- [ ] 缺少公开 TSF 模式项时，采用 Shell 原图与任务栏 UIA 点击，见 [0051](./adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)。独立模块已实现并通过 .NET 与 Unity Mono 探针，待 0050 验收后启用；系统 DLL 图像适配仍仅覆盖精确验证的版本，详见[适配说明](./notes/input-mode-shell.md)。
- [ ] 回车模块地址已修正，DOM、pointer、touch 的空闲与组字路由回归通过。真实 VR 页面仍需核对宿主答复、提交、音效、触觉与最终发送，见[回车](./notes/return-key.md)。
- [ ] 输入法列表已按系统枚举顺序显示，独立 STA 切换线程语言至中文、英文、日语时身份序列一致。真实页面仍需核对同语言与跨语言切换后的顺序和选中项，见[输入法列表顺序](./notes/ime-profile-order.md)。
- [ ] JIS 物理布局已隐藏中英切换按钮，ANSI、ISO 沿用原条件，布局切换与重建回归通过。真实 VR 页面仍需核对布局通知与空格宽度，见 [0049](./adr/0049-hide-the-input-mode-switch-on-jis.md) 与[JIS 物理布局](./notes/jis-layout-switch.md)。
- [ ] VRC ChatBox 实时广播已实现并通过本地回归，待 SteamVR 会话验收页面、宿主与 VRChat 的完整往返；决策见 [0052](./adr/0052-broadcast-chatbox-previews-and-keep-native-submission.md)，实测与验收见[VRC ChatBox](./notes/vrchat-chatbox.md)。

## XSOverlay 侧问题，不由本插件修复

- JIS 平／片假与変換的扫描码字符串 `70/79` 未转换成功后被保留，XSOverlay 送键时又按十进制虚拟键码解释为 `F/O`。安装布局、游戏解析与真实页面事件已闭合；该部分仅记录，不修复，也不继续调查修复方案。此前用户观察的 `f/a` 目标为 VSCode，其中 `a` 的完整链路仍未确认，见[XSOverlay 侧缺陷](./notes/upstream-xs-overlay.md)。
- 输入框失焦时，Alt+Shift+F 在 VSCode 中只打出大写 F。最后三次对照确认 VR 粘滞和保持都被识别为 AltRight / AltGraph，Z 丢失 Alt 修饰；物理 Alt+Z 正常。该段走 XSOverlay 原始模拟器，左 Alt 被错误标为扩展键；另报的 Alt 状态反转尚未再次复现，见[XSOverlay 侧缺陷](./notes/upstream-xs-overlay.md)。
