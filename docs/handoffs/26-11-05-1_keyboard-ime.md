# Handoff — XSOverlay 键盘接入系统输入法（2026-11-05）

本文件接续上一版交接。仓库里只有骨架插件，功能代码尚未开始；决策已全部落成 ADR。

## 1. 硬约束（来自用户，未变）

1. 以 Unity modloader（BepInEx）注入，不额外跑一个外置程序
2. 不使用轮询
3. 全部用 C#
4. 候选显示在 VR 键盘上，可点选
5. 只走 TSF，不直接调用 librime／rime.dll
6. 输入法只在键盘自带的输入框激活时生效，其余情况走原路径（新增，见 [0007](../adr/0007-composer-scope.md)）

## 2. 已落盘

`docs/adr/` 下 0001–0013 共十三条，全部 accepted；`CONTEXT.md` 是术语表。要点：

- 0001 只走 TSF；0002 BepInEx 进程内注入 + C#；0003 Mono 用经典 CCW
- 0004 候选界面靠补丁 StreamingAssets；0005 事件驱动不轮询
- 0006 组字宿主与三条取数路线；0013 组字串依赖行内预编辑
- 0007 接管范围；0008 写回方式；0009 输入法作用域；0010 候选靠喂键
- 0011 只写输入框不自动发送；0012 中英键运行时合成

## 3. 实测结论（都已验证，勿重复试）

### 能用的

| 能力       | 手段                                                    | 实测结果                        |
| ---------- | ------------------------------------------------------- | ------------------------------- |
| 候选       | `ITfUIElementMgr` + `ITfUIElementSink`                  | `count=6 sel=0`，含 emoji 候选  |
| 组字串     | 宿主窗口文本 或 `ImmGetCompositionStringW(GCS_COMPSTR)` | `ni hao`（12 字节），两条路一致 |
| 提交文本   | `GCS_RESULTSTR` 或窗口文本                              | `你好`                          |
| 喂键       | 全局 `SendInput` + 每次按键前断言前台                   | 可靠                            |
| 输入法枚举 | 老接口 `ITfInputProcessorProfiles`                      | 可列出输入法与描述              |

组字串与提交文本只有在输入法开启行内预编辑时才有内容。小狼毫对应 `style/inline_preedit: true`；关闭时组字期间窗口文本时有时无（一次读到 `ni`、一次只读到空格），IMM 返回空或垃圾。

### 不能用的（别再试）

| 尝试                                                                             | 结果                                                                                      |
| -------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| 在 RichEdit 的 context 上挂 `ITfTextEditSink` / `ITfContextOwnerCompositionSink` | `0x80040202`（TS_E_NOOBJECT），候选正在显示时也一样；`GetBase` 与 `GetTop` 是同一 context |
| `ITfKeystrokeMgr::KeyDown` 喂键                                                  | `eaten=false`，组字进行中同样不吃键                                                       |
| `ITfInputProcessorProfileMgr`                                                    | 本进程 QI 返回 `E_NOINTERFACE`                                                            |
| 读小狼毫候选窗                                                                   | `WeaselPanel` 用 d2d1/DWrite 自绘，无子窗口，`GetWindowText` 无效                         |
| `RequestEditSession`                                                             | 自我重入，会 `TF_E_LOCKED` 或卡死                                                         |

### 探针容易踩的坑

- 小狼毫的英文／中文状态跨进程持久，Shift 会来回翻。可靠判定是候选 UI 元素是否出现，不要靠按 Shift 猜。
- `BeginUIElement` 回调比按键晚到一拍，判定组字必须等下一拍。
- `SendInput` 的 `INPUT` 结构在 x64 必须是 40 字节（type 4 + 对齐 4 + union 32），写错会直接返回 0。
- XSOverlay 启动后会抢前台，每次按键前都要重新断言，并给宿主窗口加 `WS_EX_TOPMOST`。
- 反 P/Invoke 里抛异常会崩进程；COM 回调里只入队，不阻塞。

## 4. 插件实现路线（按序）

1. TSF 宿主骨架：STA 线程、隐藏 RichEdit、`ITfUIElementSink`、IMM 读取、事件队列串行化
2. 按键接管：Harmony patch 私有方法 `InputManager.HandleFocusedWebViewKeypress`，条件为 `KeyboardFocus.HasFocus` 且 `OwnerHandle` 等于键盘 overlay 的 handle
3. 写回：`KeyboardFocus.TargetWebView` 上执行 JS，`document.activeElement` 加 `document.execCommand('insertText')`，写入前先校验焦点仍在目标输入框
4. 键盘页补丁与候选栏

副作用需记录：组字期间要抢占前台窗口，结束再还回去，恢复失败要写日志。

## 5. 键盘页事实（补丁时必须知道）

- 输入框是 `#keyboard-voice-input`，focus 时请求键盘焦点，回车触发发送；它的 `placeholder` 被语音状态逻辑占用，组字期间需独占、结束后交还
- 容器与工具栏由 `buildSkeleton` 每次新建，布局更新会清空重建，塞进去的节点会被冲掉
- 射线命中白名单只认 `.key`、`.keyboard-control`、`.dropdown-button`、`.dropdown-item`，且元素必须在容器内
- 圆角规则：面板半径 = `keyGroupsPadding + 8`，内部控件半径 = 面板半径 − 内边距，两者同心
- `KeyboardFormat` 有 0/1/2 三套（完整、极简导航、带小键盘）；`mainGroup` 另有 ANSI/ISO/JIS 三种物理布局，导航与数字键区是单套
- 补丁面：`keyboard.js`（import、消息分支、射线白名单）、`keyboard-view.js`（传参）、`keyboard-view-skeleton.js`（建栏）、新增 `js/keyboard-ime.js`；样式由模块运行时注入，`Keyboard.html` 与 `keyboard.css` 不动

## 6. 界面定稿

- 候选栏常驻一行，高 64px，位于工具栏下方、键区上方，用容器直接子节点
- 左侧候选按页内序号点选；右侧是输入法下拉与方形翻页键
- 中英键合成进布局数据，插在左 Alt 右侧，从空格键扣等宽；JIS 布局不插
- 高亮用提亮底色加 2px 内描边，序号保持中性色，避免与描边同色糊在一起

## 7. 复现界面示意的方法

真实键盘页是 API 驱动的，静态打开不渲染。可起一个本地静态服务托管键盘页副本，同时在同端口减一的位置起一个假 WebSocket 服务；页面连接后依次推送主题、设置、键盘配置、覆盖层状态与布局五条消息即可渲染。布局消息里的 `mainKeys` 直接取游戏安装目录布局文件的对应行，其余键区取 `rows` 字段。

## 8. 环境

- .NET SDK 10.0.400；出网需代理
- 游戏在 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`，无 VR 也能启动
- 构建：`dotnet build -c Release`；部署加 `-p:DeployToGame=true`
- 探针工程、启动脚本、界面副本与游戏备份都在仓库内的 temp/，已被 git 忽略；temp/tools/ 有部署、启动探针与快照脚本，可直接复用
- 小狼毫的配置位于 %APPDATA%\Rime，属于本机环境，相关改动与备份都不入库

## 9. 下一步

先做第 4 节第 1 条，只验证骨架能组字、能读候选、能读组字串与提交，不碰 UI。做完再决定是否先补 UI 还是先接通写回。
