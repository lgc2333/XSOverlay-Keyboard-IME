# 候选栏与组字串上屏

接续 [26-11-06-3](./26-11-06-3_ui-mock.md)。这次把界面落地：组字串与候选真正显示到键盘页，点选候选与翻页接上组字线程。

## 做了什么

| 改动       | 内容                                                                                                                                           |
| ---------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| 组字串显示 | 整串写进 composer 输入框的 `placeholder`，`value` 始终不动，发送键因此保持禁用；结束时把页面原本的文案还回去                                   |
| 覆写回压   | 页面自己的状态逻辑会重写 `placeholder`，注入脚本盯住该属性并在组字期间重新压回组字串，页面重建后也照样生效                                     |
| 候选栏     | 注入为 `#keyboard-container` 的直接子节点，位置在 `#keyboard-toolbar-row` 与 `#keyboard-background` 之间，高 64px                              |
| 重建补挂   | `buildSkeleton` 每次重建整棵树，注入脚本在容器不再是自己的父节点时把栏插回去                                                                   |
| 点选与翻页 | 页面按 `xso-keyboard-ime pick <序号>` 与 `turn ±1` 回传，插件喂键：Rime 走页内数字键（前九项），其余情况走方向键加回车，翻页走 PageUp/PageDown |
| 输入法名   | 取 `ITfInputProcessorProfiles` 中当前语言的默认配置描述，用作右侧 chip；取不到时 chip 隐藏、点选退回方向键                                     |
| 页面通道   | 静态 `KeyboardPageBridge` 换成 `Page/KeyboardPage`（页面实例）、`PageScript`（bundle 与注入）、`CompositionPayload`、`BarAction`（回传解析）   |

组字串与候选由 `CompositionService` 在每次 IME 更新后发布成 `CompositionView`，经主线程队列送到页面；会话结束发布一个带输入法名的空视图，chip 不会闪。

## 离线渲染台

游戏外验证界面靠一套最小的无头浏览器渲染台，不进仓库（页面副本是游戏资源）：

- 静态服务把游戏 `UserInterface/Default/Keyboard/Keyboard.html` 的副本当作网站根目录，必须按这个深路径打开；开 `/` 会让页内 `./js/...` 全部 404，页面全白且控制台不报错。
- 假 WebSocket 服务（静态端口减一）连上后依次推送主题、设置、配置、状态、布局五条消息，两秒后再推一次布局，用来验证整树重建后候选栏自己回来。
- 无头 Chrome 经 CDP 打开该页，用 `Runtime.evaluate` 把 `web/dist/keyboard-ime.js` 当普通脚本执行一次，与游戏里的 `ExecuteJavaScript` 同路；再注入一次不会多出第二条候选栏。
- 断言靠读 DOM：`#keyboard-ime-bar` 条数、候选节点数、`placeholder` 与 `value` 的值；外观靠截图，截图前把 `body` 背景设深色，否则是白底。

## 已验证

- 离线渲染台跑真实注入 bundle：placeholder 显示组字串、`value` 为空、候选栏位于工具栏行与键区之间、选中项带 2px 内描边。
- 页面回写 `placeholder` 后再读回，仍是组字串；再发一条布局消息触发整树重建，候选栏与组字串都自动回来。
- 打包产物不含 import/export 语句，可以直接被 `ExecuteJavaScript` 当普通脚本执行。
- `dotnet build`（含自检变体）、49 项测试、CSharpier、Prettier、`pnpm --dir web typecheck`、`lint`、`build` 全部通过。

## 未验证与已知限制

- 输入法名读取与数字键点选还没在游戏里跑过；离线渲染台用固定数据代替插件推送。
- 默认配置不等于线程活动配置：用户在同一语言下装了多个输入法且默认项不是正在用的那个时，chip 名字会不准。`ITfInputProcessorProfileMgr` 在本进程不可用，精确获取需要旁听 `ITfActiveLanguageProfileNotifySink`。
- 非 Rime 输入法的方向键点选只有代码路径，没有实测。
- 候选栏不在键盘页的射线白名单里，悬停高亮与触感都拿不到；点击不受影响，因为页面自己的按键也走 `pointerdown`。
- 点选后写回依赖 composer 仍有焦点：`pointerdown` 已取消默认动作，离线渲染台里焦点保留，VR 里未实测。
- chip 目前只显示名字，没有下拉；中英键尚未合成进布局。
- 组字会话期间仍一直持有前台租约，到提交键才交还（旧限制，未变）。

## 下一步

1. 中英键与 chip 下拉，依赖 [0009](../adr/0009-profile-scope.md) 与 [0012](../adr/0012-synthesized-modifier-key.md)。
2. 用 `ITfActiveLanguageProfileNotifySink` 旁听真正激活的配置，替掉"默认配置"这个替代品。
3. 抽 `CompositionService` 与 `SelfCheckRun` 里重复的组字泵。
4. 在 VR 里按一次回车提交与一次候选点选，补上真机结论。
