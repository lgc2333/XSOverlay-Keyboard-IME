# 路线图

## 当前实施范围

仓库已进入实现阶段。当前行为约束以已接受的 ADR 为准，实现情况以源码、测试和下列待办为准。前期[调查笔记](./handoffs/archived-notes/README.md)已归档，仅供追查证据与决策背景。

- 前台与重开：实现 [0050](./adr/0050-hold-the-host-focus-for-the-composer.md)、[0055](./adr/0055-use-one-alt-pair-after-a-denied-focus-request.md)、[0058](./adr/0058-allow-refocus-rejection-during-revocation.md) 与 [0060](./adr/0060-create-refocus-requests-when-page-input-arrives.md)。第一、二项共用送键资格与撤销机制。
- 文字与编辑：按 [0054](./adr/0054-write-ordinary-text-through-host-results.md) 分流宿主文字和浏览器原生编辑，验证跨接口的完成顺序。
- 结果与显示：按 [0057](./adr/0057-apply-ime-results-to-the-current-composer.md) 交付当前聚焦输入框，按 [0056](./adr/0056-show-preedit-in-a-composer-tab.md) 独立显示预编辑。
- 原生清理：按 [0059](./adr/0059-use-one-transitory-document-for-ime-results.md) 使用单一 transitory 文档，并完成异步回调与资源收尾。

## 待验证

- [ ] 键盘首次聚焦与输入法切换修复已接线：持续前台、正常获取、受限一次 Alt、旧请求撤销及明确操作重开均已实现，离线回归与双轴审阅已完成。尚未部署；统一验收真实 Vuplex／Unity／STA 往返、获取失败、自然失焦、关闭、重载及同步后重新操作。

- [ ] 微软中文与日语输入修复已接线：持续前台、宿主文字与原生编辑排序、当前焦点丢弃规则、独立预编辑与单一 transitory 后端均已实现，本地回归及双轴审阅已完成。尚未部署；统一验收中文／日语连续转换、候选、回车、原生编辑与撤销、失焦重开，以及预编辑栏的 VR 尺寸。真实 Vuplex 按键与完成通知的顺序、AltGr 布局文字和输入法切换仍需现场验证。

- [ ] 回车首次宿主请求已与原模块加载解耦，15 项专项回归通过，保留原生重复、释放与反馈。尚未部署；按 [0047](./adr/0047-route-page-consumed-return-through-the-host.md) 与 [0052](./adr/0052-broadcast-chatbox-previews-and-keep-native-submission.md) 验收真实宿主答复、提交、音效、触觉与最终发送。
- [ ] 缺少公开 TSF 模式项时，采用 Shell 原图与任务栏 UIA 点击，见 [0051](./adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)。独立模块已实现并通过 .NET 与 Unity Mono 探针，待 0050 验收后启用；系统 DLL 图像适配仍仅覆盖精确验证的版本，详见[适配说明](./handoffs/archived-notes/input-mode-shell.md)。
- [ ] 输入法列表已按系统枚举顺序显示，独立 STA 切换线程语言至中文、英文、日语时身份序列一致。真实页面仍需核对同语言与跨语言切换后的顺序和选中项，见[输入法列表顺序](./handoffs/archived-notes/ime-profile-order.md)。
- [ ] JIS 物理布局已隐藏中英切换按钮，ANSI、ISO 沿用原条件，布局切换与重建回归通过。真实 VR 页面仍需核对布局通知与空格宽度，见 [0049](./adr/0049-hide-the-input-mode-switch-on-jis.md) 与[JIS 物理布局](./handoffs/archived-notes/jis-layout-switch.md)。
- [ ] VRC ChatBox 实时广播已实现并通过本地回归，待 SteamVR 会话验收页面、宿主与 VRChat 的完整往返；决策见 [0052](./adr/0052-broadcast-chatbox-previews-and-keep-native-submission.md)，实测与验收见[VRC ChatBox](./handoffs/archived-notes/vrchat-chatbox.md)。

## XSOverlay 侧问题，不由本插件修复

- JIS 平／片假与変換的扫描码字符串 `70/79` 未转换成功后被保留，XSOverlay 送键时又按十进制虚拟键码解释为 `F/O`。安装布局、游戏解析与真实页面事件已闭合；该部分仅记录，不修复，也不继续调查修复方案。此前用户观察的 `f/a` 目标为 VSCode，其中 `a` 的完整链路仍未确认，见[XSOverlay 侧缺陷](./handoffs/archived-notes/upstream-xs-overlay.md)。
- 输入框失焦时，Alt+Shift+F 在 VSCode 中只打出大写 F。最后三次对照确认 VR 粘滞和保持都被识别为 AltRight / AltGraph，Z 丢失 Alt 修饰；物理 Alt+Z 正常。该段走 XSOverlay 原始模拟器，左 Alt 被错误标为扩展键；另报的 Alt 状态反转尚未再次复现，见[XSOverlay 侧缺陷](./handoffs/archived-notes/upstream-xs-overlay.md)。
