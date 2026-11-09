# 输入修复调查归档

> 历史归档（2026-11-10）：保留调查时的事实、方案与待验证项。当前实现以源码、测试、已接受的 [ADR](../../adr/) 和[路线图](../../ROADMAP.md)为准。

本目录保存已结束的主题调查，供追查证据与决策背景。下文的状态、计划和待办均保留归档时的表述，不再作为活跃笔记更新。当前规则见已接受的 [ADR](../../adr/)，当前任务见[路线图](../../ROADMAP.md)。

五项新报告的调查见[2026-11-10 路线图缺陷](roadmap-defects-2026-11-10.md)。

结果处理现以 [0057](../../adr/0057-apply-ime-results-to-the-current-composer.md) 为准；0053 已被取代，原目标隔离实验保留为历史证据。复杂来源与注入编辑接管的扩展调查已停止，0050 的焦点安全要求仍待实现。

## 六项缺陷

| 问题                 | 已确定来源                                                        | 状态                                                                               |
| -------------------- | ----------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| 微软拼音、日语无输入 | 插件过早归还前台；迟到普通字符未交付                              | 独立宿主对照已确认；生命周期未实现，物理输入与真实迟到提交待验证                   |
| JIS 专用键           | XSOverlay 把残留扫描码 `70/79` 当作虚拟键码；無変換 `a` 来源未定  | 标准码接管已实现；上游错误仅记录，见 [upstream-xs-overlay](upstream-xs-overlay.md) |
| 回车无反应           | 插件模块 URL 缺少 `/apps`；修正后现场仍在页面事件至宿主请求间中断 | 地址源码已修正；首次请求与模块加载解耦待实现，真实 VR 未通过                       |
| 输入法列表重排       | 插件枚举时把当前语言放在前面                                      | 已删除该分支，独立 STA 对照通过，待真实页面验收                                    |
| JIS 出现切换按钮     | 插件没有使用物理布局限制                                          | 已按 [0049](../../adr/0049-hide-the-input-mode-switch-on-jis.md) 实现，待 VR 验收  |
| 首次切换无效         | 初始请求的原生前台调用直接失败                                    | 0055受限一次Alt已接受；完整0050与VR未闭合，来源扩展调查停止                        |

## 实施顺序

回车地址、列表顺序、JIS 按钮与标准码接管已实现。微软输入修复的[第一项方案](ime-input-repair-plan.md)与[第二项方案](foreground-repair-plan.md)已记录 0058／0059／0060 取舍；重开握手、事件覆盖及编辑完成候选仍待实际接线验证，产品实现尚未授权。不要求实现前完成全部真机验收，剩余条件见[整体对账](foreground-status.md)。

## 笔记

- [host-lifecycle](host-lifecycle.md) — 前台租约、物理输入与验收。
- [late-commit](late-commit.md) — 当前结果边界与迟到提交的历史证据。
- [context-isolation](context-isolation.md) — 原目标隔离的历史证据与资源回收缺口。
- [foreground-status](foreground-status.md) — 第二项整体对账、未闭合条件和下一步。
- [foreground-repair-plan](foreground-repair-plan.md) — 第二项四块最小方案、实施接缝与集中行为取舍。
- [foreground-refocus-protocol](foreground-refocus-protocol.md) — 0060页面操作边界下的清理确认、重开及重复消息规则。
- [foreground-acquire](foreground-acquire.md) — 首次前台获取；[接线评审](foreground-acquire-review.md)记录来源与寿命缺口；[入口守卫](foreground-acquire-guards.md)区分临时探针拒绝与系统拒绝。
- [foreground-action-order](foreground-action-order.md) — 安装版Vuplex输入/JS逆序、TouchID与完成边界。
- [foreground-direct-injection](foreground-direct-injection.md) — 未落地的注入候选、实际拦截点与原生编辑缺口。
- [foreground-editing-baseline](foreground-editing-baseline.md) — 原生光标、选区和导航的历史基线；接管责任未采纳。
- [foreground-injection-spike](foreground-injection-spike.md) — 同源显式操作的13例对照及控件外拖选、RTL、失败focus反例。
- [foreground-injection-callbacks](foreground-injection-callbacks.md) — 同步回调的条件性覆盖反例；实际入口再入未证实。
- [foreground-pointer-source](foreground-pointer-source.md) — Begin入队前的来源冻结、设备实例与后续阶段继承。
- [return-key](return-key.md) — 回车。
- [ime-profile-order](ime-profile-order.md) — 输入法列表顺序。
- [jis-layout-switch](jis-layout-switch.md) — JIS 物理布局隐藏切换按钮。
- [jis-ime-keys](jis-ime-keys.md) — JIS 专用键。
- [vrchat-chatbox](vrchat-chatbox.md) — VRC ChatBox 实时广播。
- [upstream-xs-overlay](upstream-xs-overlay.md) — XSOverlay 侧缺陷。
- [input-mode-shell](input-mode-shell.md) — Shell 模式项适配。
- [release-and-recovery](release-and-recovery.md) — 部署、恢复与当前基线。
