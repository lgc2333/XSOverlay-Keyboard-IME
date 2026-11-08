# 路线图

## 当前待解决

六项输入缺陷的来源、实施状态与验收条件见[落地方案](./roadmap-input-fixes.md)。宿主随 composer 聚焦持有前台，失焦遵循输入法默认行为，见 [0050](./adr/0050-hold-the-host-focus-for-the-composer.md)；生命周期实现与验证尚未完成。

- [x] 按语言栏附加图标的有无显示切换键，公开 TSF 模式项优先，缺项时读取 Shell 原图或字形。原图不可读时使用占位，美式布局隐藏，点击不可用时保持 disabled。见 [0042](./adr/0042-hide-the-switch-without-an-icon.md)。
- [ ] 缺少公开 TSF 模式项时，采用 Shell 原图与任务栏 UIA 点击，见 [0051](./adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)。独立模块已实现并通过 .NET 与 Unity Mono 探针，待 0050 验收后启用；系统 DLL 图像适配仍仅覆盖精确验证的版本，详见[适配说明](./input-mode-shell.md)。

- [ ] 微软中文与日语输入法在输入框为焦点时打不出任何字，不管是中文还是英文模式，打不出一个字，没有预编辑，没有候选，没有一个字上屏输入框。英文模式也如此。独立生产宿主对照已重复：原样归还前台时无输入，仅抑制归还后拼音开始组字、日语收到普通字符，支持插件过早归还前台；提权游戏内的干预尚未完成。日语迟到字符未立即交付也是插件缺陷，见[调查记录](./handoffs/26-11-08-1_roadmap-input-defects.md#微软拼音与日语输入法无输入)。
- [ ] 微软日语的 JIS 专用键异常：平片假只出现预编辑的 f，非变换只出现预编辑的 a。标准 `KANA/CONVERT/NONCONVERT` 的原码接管已实现；XSOverlay 现场实际提供 `70/235`，仍需核对真实按键链路。边界见 [0048](./adr/0048-route-jis-ime-keys-through-the-host.md) 与[调查记录](./handoffs/26-11-08-1_roadmap-input-defects.md#jis-专用键)。
- [ ] 键盘在首次打开并聚焦输入框时，切换输入法无效。点击一次桌面浮窗中的其他窗口后恢复正常。已捕获前两次请求到达宿主但取前台失败、焦点往返后成功的记录，拒绝或前台被夺回的原因仍待区分，见[调查记录](./handoffs/26-11-08-1_roadmap-input-defects.md#首次聚焦后切换无效)。

## 待真机验证

- [ ] 回车模块地址已修正，DOM、pointer、touch 的空闲与组字路由回归通过。真实 VR 页面仍需核对宿主答复、提交、音效、触觉与最终发送。
- [ ] 输入法列表已按系统枚举顺序显示，独立 STA 切换线程语言至中文、英文、日语时身份序列一致。真实页面仍需核对同语言与跨语言切换后的顺序和选中项。
- [ ] JIS 物理布局已隐藏中英切换按钮，ANSI、ISO 沿用原条件，布局切换与重建回归通过。真实 VR 页面仍需核对布局通知与空格宽度，见 [0049](./adr/0049-hide-the-input-mode-switch-on-jis.md)。

## 下一步

- [ ] 优化 VRC ChatBox 体验，行为约定、接入线索与桌面实测见[调查交接](./handoffs/26-11-08-3_vrchat-chatbox-investigation.md)。

## XSOverlay 侧问题，不由本插件修复

- [ ] 输入框失焦时，Alt+Shift+F 在 VSCode 中只打出大写 F。最后三次对照确认 VR 粘滞和保持都被识别为 AltRight / AltGraph，Z 丢失 Alt 修饰；物理 Alt+Z 正常。该段走 XSOverlay 原始模拟器，左 Alt 被错误标为扩展键；另报的 Alt 状态反转尚未再次复现，见[调查记录](./handoffs/26-11-08-1_roadmap-input-defects.md#失焦时-altshiftf)。
