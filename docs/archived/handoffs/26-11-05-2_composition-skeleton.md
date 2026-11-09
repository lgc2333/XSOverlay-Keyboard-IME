# Handoff — 组字骨架落地（2026-11-05，第二段）

接续 [26-11-05-1](./26-11-05-1_keyboard-ime.md)。本段按第 4 节第 1 条把 TSF 组字骨架写进插件，未碰键盘页 UI。

## 1. 完成

- 工程布局：插件与测试工程都在仓库根；测试工程直接编译无依赖的源文件。
- 组字宿主：STA 线程、屏幕外宿主窗口、RichEdit50W 子窗口、`CoInitializeEx(APARTMENTTHREADED)`。
- 事件驱动：`WM_APP+1` 拍、`WM_APP+2` UI 元素事件、`WM_APP+3` 二次观察；全仓没有定时器（[0005](../../adr/0005-event-driven.md)）。
- 候选、组字串与提交文本按 [0006](../../adr/0006-composition-host.md) 的路线读取；行内预编辑降级按 [0013](../../adr/0013-inline-preedit-dependency.md) 实现。
- 自检：`SelfCheck/Enabled` 打开后等 8 秒再跑，最多三次尝试，退出时交还前台。

## 2. 实测结果

| 能力               | 结果                                        |
| ------------------ | ------------------------------------------- |
| 宿主窗口           | 屏幕外，`WS_EX_TOOLWINDOW`，不外露          |
| TSF 线程管理器     | 取到，`ITfUIElementMgr` 与 `ITfSource` 可用 |
| `ITfUIElementSink` | `AdviseSink` 返回 0                         |
| 前台交还           | 结束时恢复                                  |

未能验证：组字没有开始，按键没有进入宿主编辑框。

## 3. 关键发现：XSOverlay 的 Ghost 窗口

前台窗口的类名是 `Ghost`，模块是 `XSOverlay.exe`。它持续占用前台，以下手段全部无效：

- `SetForegroundWindow`
- `SwitchToThisWindow`
- `AttachThreadInput` 加合成 Alt
- 隐藏该窗口后重试

隐藏之后它仍被重新置前台。焦点能设到宿主编辑框（`GetFocus` 等于编辑框句柄），但按键按前台线程分发，所以进不了宿主。

这意味着 [0006](../../adr/0006-composition-host.md) 的"全局 SendInput 加抢占前台"前提在当前 XSOverlay 版本下不成立，至少不能靠常规 API 实现。上一段的原型验证过抢前台可行，当时的窗口状态与本段不同，不能作为反证。

## 4. 下一步（需决策）

- **接管 Ghost 的前台循环**：用 Harmony 停掉它的置前台逻辑，或把宿主窗口挂成它的子窗口。侵入性强，需要先弄清 Ghost 的职责。
- **改走文本存储路线**：在 Ghost 窗口上挂 TSF 文本服务（`ITextStoreACP`，[0006](../../adr/0006-composition-host.md) 的否决项）。不抢前台，代价是上千行代码。
- **先确认目标场景**：VR 会话中 XSOverlay 的前台行为可能与桌面启动不同。

本段没有改动任何 ADR。
