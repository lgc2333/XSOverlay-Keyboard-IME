# 接管与写回落地

接续 [26-11-05-3](./26-11-05-3_vr-verification.md)。这次把按键接管与写回写进插件，页面侧改为运行时注入，并按 [ADR 0004](../adr/0004-inject-page-script.md) 的新结论建了前端工程。

## 做了什么

| 改动     | 内容                                                                                                                                                          |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 前端工程 | `web/`：TypeScript 6、ESLint 10 + `@antfu/eslint-config`、Prettier；esbuild 打成 `web/dist/keyboard-ime.js`，构建时作为嵌入资源进 DLL                         |
| 页面脚本 | `#keyboard-voice-input` 写回（`execCommand('insertText')`，写前校验焦点）、`window.vuplex.postMessage` 上报通道、`window.__xsoKeyboardIme` 入口               |
| 接管     | Harmony 前缀补丁打在 `XSOverlay.InputManager.HandleKeypress`；条件是 `KeyboardFocus.HasFocus` 且 `OwnerHandle` 等于 `Overlay_Manager.Keyboard_Overlay.handle` |
| 组字     | `CompositionService`：逐键喂给常驻宿主，交给输入法自己提交；`ImeHost` 统一拥有 STA 线程、宿主窗口、前台租约与 TSF 桥                                          |
| 写回     | 提交文本经主线程队列交给 `KeyboardPageBridge`，在键盘页执行 `insertText`（注入与调用在同一条脚本里，页面重载后自动重注入一次）                                |
| 决策     | [ADR 0004](../adr/0004-inject-page-script.md) 改写为运行时注入；回车语义按用户决定：组字中喂给输入法，非组字放行原逻辑                                        |
| 自检隔离 | 自检改为条件编译：代码只在 `SelfCheck/` 下，用 `-p:SelfCheck=true` 才进入构建；生产 DLL 不含任何自检符号                                                      |

## 已验证

- `dotnet build -c Release` 通过；`dotnet test` 17 项通过；`dotnet csharpier check .` 干净。
- `pnpm --dir web typecheck`、`lint`、`prettier --check`、`build` 全部通过。
- 部署后的 DLL 内确认含嵌入资源 `keyboard-ime.js`。
- 旧版本上自检仍在 VR 会话里跑通（日志：`committed text '你好'`）。

## 决定的批准

- [ADR 0004](../adr/0004-inject-page-script.md) 由旧结论就地改写为新结论，用户明确批准，不走 supersede 另开新号。
- 补丁点与按键语义按用户批准单独立 [ADR 0016](../adr/0016-key-interception-point.md)。

## 审查后的修正

两轴代码审查（标准 / 规格）后补的改动：

| 问题                                              | 改法                                                                                               |
| ------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| 自检字段与分支仍留在 `Host/`、`KeyboardImePlugin` | 自检半边改成 `SelfCheck/` 下的 partial 类与配置绑定；生产构建用 csproj `Compile Remove` 整目录排除 |
| 回车判据用"打算组字"，不是"已在组字"              | 接管控制键改看 `ConfirmedComposing`（IME 已发布 UI 元素）；非组字时回车与空格都走 XSOverlay 原逻辑 |
| 页面消息只发不收                                  | 接管首批按键时注入，并订阅 `IWebView.MessageEmitted` 记录页面消息                                  |
| 注入发生在写回时，不是接管时                      | 注入提前到接管首次用到键盘页时，符合 [ADR 0004](../adr/0004-inject-page-script.md)                 |
| 缺 `web/dist` 时静默产出无脚本的 DLL              | csproj 加 `RequirePageBundle` 目标，缺包直接构建失败                                               |
| 宿主类名 `ImeHost` 用了 CONTEXT 明确避免的 "IME"  | 改回 `CompositionHost`                                                                             |
| README 承诺候选栏                                 | 开头改成只写已实现的能力，并补 `Takeover/Enabled` 停用方式                                         |

## 未验证（需要重启后的 VR 会话）

- 补丁是否真的挂上（预期日志 `take-over patched onto XSOverlay.InputManager` 与 `take-over ready`）。
- 组字中按键是否被正确接管，以及写回是否落进 composer 输入框。
- 每次提交都会交还前台、下一个键再抢回，可能造成前景抖动；若明显，改成整段会话持有租约。

## 注意

- 自检只在诊断构建里存在：`dotnet build -c Release -p:SelfCheck=true`。正式构建里没有 `SelfCheck` 配置项，也没有 `SelfCheckRun` 类型；生产 DLL 已核验不含自检符号。
- `pnpm --dir web build` 在非交互环境要先 `CI=true pnpm --dir web install`，否则 pnpm 会因无法确认清理 `node_modules` 而失败。
- 前端只改页内节点，不改 XSOverlay 目录里的任何文件。
- Harmony 前缀补丁按参数名绑定。XSOverlay 的 `HandleKeypress` 参数名是 `e`，按名字写会报 `Parameter "keyEvent" not found` 并整条补丁失效，因此改用位置参数 `__0`。插件在挂补丁后校验 `Harmony.GetPatchInfo`，失败时写错误日志而不是静默。

## 未做

- 候选栏（界面、点选、翻页），以及 [ADR 0010](../adr/0010-candidate-by-keys.md) 的喂键翻页与选择。
- 中英键合成进布局（[ADR 0012](../adr/0012-synthesized-modifier-key.md)）。
- 候选与组字串显示到页面（现在只写日志）。
- `CompositionService` 与 `SelfCheckRun` 的元素读取逻辑仍有重复，待抽公共的组字泵。
- 补丁点与按键语义见 [ADR 0016](../adr/0016-key-interception-point.md)。
