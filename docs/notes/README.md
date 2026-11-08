# 输入修复笔记

本目录记录六项输入缺陷的来源、实施与验收。规则与决定以 [ADR](../adr/) 为准，当前待办以[路线图](../ROADMAP.md)为准。笔记只写当前有效的事实与待验证项，不重复 ADR 的论证。

## 六项缺陷

| 问题                 | 已确定来源                                                       | 状态                                                                               |
| -------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| 微软拼音、日语无输入 | 插件过早归还前台；迟到普通字符未交付                             | 独立宿主对照已确认；生命周期未实现，物理输入与真实迟到提交待验证                   |
| JIS 专用键           | XSOverlay 把残留扫描码 `70/79` 当作虚拟键码；無変換 `a` 来源未定 | 标准码接管已实现；上游错误仅记录，见 [upstream-xs-overlay](upstream-xs-overlay.md) |
| 回车无反应           | 插件模块 URL 缺少 `/apps`；回归服务器掩盖错误                    | 已修正，六条页面路由回归通过，待 VR 验收                                           |
| 输入法列表重排       | 插件枚举时把当前语言放在前面                                     | 已删除该分支，独立 STA 对照通过，待真实页面验收                                    |
| JIS 出现切换按钮     | 插件没有使用物理布局限制                                         | 已按 [0049](../adr/0049-hide-the-input-mode-switch-on-jis.md) 实现，待 VR 验收     |
| 首次切换无效         | 初始请求的原生前台调用直接失败                                   | 已区分拒绝与夺回；拒绝条件待确定                                                   |

## 实施顺序

回车地址、列表顺序、JIS 按钮与标准码接管已实现。宿主生命周期按 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md) 接受，先完成独立原型与物理输入验证，再实施微软输入修复。首次前台获取保持独立诊断；租约寿命调整不能替代该实验。

## 笔记

- [host-lifecycle](host-lifecycle.md) — 前台租约、物理输入与验收。
- [late-commit](late-commit.md) — 迟到提交、普通字符来源与失焦写回。
- [context-isolation](context-isolation.md) — 多上下文隔离、候选归属与回收。
- [foreground-acquire](foreground-acquire.md) — 首次前台获取。
- [return-key](return-key.md) — 回车。
- [ime-profile-order](ime-profile-order.md) — 输入法列表顺序。
- [jis-layout-switch](jis-layout-switch.md) — JIS 物理布局隐藏切换按钮。
- [jis-ime-keys](jis-ime-keys.md) — JIS 专用键。
- [vrchat-chatbox](vrchat-chatbox.md) — VRC ChatBox 实时广播。
- [upstream-xs-overlay](upstream-xs-overlay.md) — XSOverlay 侧缺陷。
- [input-mode-shell](input-mode-shell.md) — Shell 模式项适配。
- [release-and-recovery](release-and-recovery.md) — 部署、恢复与当前基线。
