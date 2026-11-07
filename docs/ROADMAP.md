# 路线图

## 已实现

- [x] 接管键盘页的按键，交给系统输入法组字。
- [x] 在输入框里显示组字串，在候选栏里显示候选。
- [x] 提交后把文本写回输入框，不自动发送。
- [x] 组字期间的按键由输入法处理，不会落到输入框上。
- [x] 输入法不在组字时，编辑键与 Ctrl、Alt 组合照旧交给键盘页；其他 Win 组合键由插件吞掉，不往输入框打字符。见 [0029](./adr/0029-modifiers-from-game-state.md)。
- [x] VR 里切换中英：键上画输入法自己的语言栏图标，按下即按它。见 [0028](./adr/0028-mirror-the-ime-language-bar-item.md)。
- [x] 候选栏只在 composer 输入框有焦点时显示当前输入法的名字与中英切换按钮，失焦即隐藏；切换键不占行宽。插件不读其他程序，也不轮询。见 [0035](./adr/0035-show-the-ime-only-while-the-composer-has-focus.md)、[0037](./adr/0037-the-page-owns-the-composer-state.md)。
- [x] 组字串的光标画在输入法报出的选区上，方向键的效果看得见。见 [0031](./adr/0031-preedit-selection-follows-the-ime.md)。
- [x] 分段组字：空格只转换光标前那一段，会话随组字结束而不是随空格结束。见 [0033](./adr/0033-end-the-session-with-the-composition.md)。
- [x] 提交晚于组字结束时照样送达页面，不再被当成「会话外提交」丢弃。见 [0032](./adr/0032-deliver-a-late-commit.md)。
- [x] 组字期间用射线点了输入框、输入框失焦、键盘页关闭时取消组字；页面重建输入框不算失焦。见 [0027](./adr/0027-cancel-composition-on-caret-move.md)、[0034](./adr/0034-a-rebuilt-composer-is-not-a-blur.md)。
- [x] 脚本注入时同步 composer 输入框已有的焦点，页面重建后仍可继续输入。
- [x] 按键归属只有一张表：页面、输入法、系统、吞掉。Shift、Ctrl、Alt 的按下与抬起实时送到系统，粘滞、保持、解除规则跟随 XSOverlay；Caps 按次发送，关闭键盘不改变锁定状态。Win 不单独送到系统，其他 Win 组合吞掉，Win+空格交给系统。见 [0040](./adr/0040-modifiers-follow-the-game.md)。
- [x] 没有可显示的语言栏图标时隐藏整个中英切换键，切换输入法时清掉旧图标，隐藏区域仍可按空格。见 [0042](./adr/0042-hide-the-switch-without-an-icon.md)。
- [x] 删除启动等待、合成 Alt、前台重试、切换后等待与下拉框忙碌提示，前台切换失败时停止本次送键。名字与图标随通知或后续按键刷新，保留用户操作触发的取消与页面重建的 60 毫秒等待。见 [0041](./adr/0041-no-recovery-for-native-ime-results.md)。

## 待真机验证

每条：怎么做 → 应该看到什么。

- [x] 冷启动与前台交接：启动后立即在 VR 输入，再让桌面窗口保持前台后重复输入、提交与取消 → 无需等待即可输入，提交与取消后恢复原前台窗口；若日志报告前台切换失败，本次按键不得落入其他窗口。
- [ ] 实际字符：输入英文小写、数字、Shift 标点，再试 Caps 与 Caps+Shift → 与相同 Windows 布局下普通输入框的结果一致。
- [ ] 聚焦不抖：点输入框、再点别处，来回几次 → 键盘行宽度不变，右 Ctrl 不被顶出，切换键只在聚焦后出现，且只有一个。
- [ ] 初始焦点保留：首次打开键盘，保持 composer 输入框已有的焦点并切换布局 → 新输入框仍有焦点，可以直接输入。
- [ ] 无图标时隐藏：切到不提供语言栏图标的输入法，再切布局、聚焦与失焦 → 切换键始终隐藏，空格原区域可点击；切回提供图标的输入法后只显示一个切换键。
- [ ] 组字中射线点输入框：打 `nihc`，射线点同一个输入框 → 组字被打断，只剩原本的文字，不留半截拼音。
- [ ] 组字中点清空：打 `nihc`，点清空 → 输入框清空，组字结束，旧文字不回填。
- [ ] 取消与页面重建：组字中点别处或关闭键盘，再分别在组字中切换键盘布局 → 真正失焦或关闭时取消组字；布局重建输入框时保留组字，并可继续输入。
- [ ] 修饰键跟随 XSOverlay：分别试左右 Shift、Ctrl、Alt 的粘滞、保持与解除，再试 Ctrl+A/C/V 与 Caps → 操作时机与未安装插件时一致，输入法收到真实的修饰键操作，组合键生效，Caps 可锁上再解开。
- [ ] 关闭键盘释放修饰键：分别让 Shift、Ctrl、Alt 进入粘滞或保持状态，再关闭键盘并重开 → 没有残留的修饰键，Caps 锁定状态不变；仅 composer 输入框失焦时按 XSOverlay 原有规则处理。
- [ ] Win 限制：单按再松开 Win，再试 Win+R → 不弹出开始菜单或桌面窗口，也不向 composer 输入框打字符，后续普通输入正常。
- [ ] 组字中按中英切换键：打 `nihc` 不选词，按空格左侧的切换键 → 结果由输入法决定，与系统输入框中的同一操作一致，插件不替它提交或取消。
- [ ] 组字中 Win+空格：打 `nihc`，按 Win+空格；再快速连按两次 → 系统切换输入法，旧组字由输入法处理，名字与图标随通知或下一次按键刷新。
- [ ] 候选栏滚动：打一个候选很多的词，PageUp/PageDown 翻到最后 → 活跃候选始终完整可见，不被渐隐挡住。
- [ ] 下拉框：空闲与组字时分别选另一个输入法；装了多种语言时也选一个别的语言的 → 当前项带勾与描边，点一条即切，点外面收起；列表里有其他语言的输入法，条目带语言名；切过去之后打的字母属于新输入法，旧组字由输入法处理，插件不替它提交或取消。
- [ ] 提交晚于组字结束：连续快打、回车提交、取消后立刻重打 → 不丢字。
- [ ] ChatBox 发送：打开 VRC 目标按钮，打一句中文发送 → 文本进 VRChat 的 ChatBox，插件不自动发送。

下一轮 VR 的顺序：冷启动立即输入 → 检查前台交接 → 点输入框 → 切到中文 → 逐条走上面的验证。对应的日志行：`feed Escape`（取消）、`feed Win+Space` 与 `the IME is now`（切换）、`feed LeftShift down` 与 `feed LeftShift up`（修饰键）、`the page switched to profile`（下拉框切换）。

## 下一步

- [ ] 优化 VRC ChatBox 体验
  - [ ] ChatBox 目标按钮打开后，组字串实时送进 ChatBox。**实现过、验证过、已撤回**：页面侧先按 800ms 节流发过 `{"text":"nihc"}`，但它与 [0011](./adr/0011-input-only-no-autosend.md)「插件不复制发送职责」冲突；更硬的阻碍是**清空不了**——XSOverlay 的 OSC 服务对空白文本直接拒绝，组字结束后半成品会留在 ChatBox 里。建议 v1 不做。
  - 输入框获得焦点时向 VRChat 上报正在输入，失焦时取消。路线：XSOverlay 的 API 没暴露 OSC typing，但插件可以在 C# 侧直接向 VRChat 的 OSC 输入端口发 `/chatbox/typing` 的 UDP 包；前提是用户开了 VRChat 的 OSC。同样要先想清楚它算不算「第二份发送职责」。
  - 已知接口：页面发 `SendVRChatChatboxText`，载荷 `{"text": "..."}`，上限 144 字符；要求 VRChat 在线且 OSC 可用（`vrChatActive` / `vrChatOscAvailable`）。「正在输入」对应 VRChat 的 OSC typing 参数，XSOverlay 的 API 目前没有暴露，需要另找入口或放弃。
  - 实现路线（已实测）：注入脚本 `import('/Shared/js/api.js')` 能拿到 `Send` 与 `Commands.SendVRChatChatboxText`，页面自己的 `keyboard.js` 用的是同一个 URL，模块按 URL 缓存，所以是同一份实例、同一条连接、同一个 overlay 身份；手动 `Send` 已经能从页面的 mock 服务器收到。
  - 目标开关：状态在 `.keyboard-vrc-toggle` 的 `aria-pressed` 上，但页面会按自己的会话状态重写这个属性，所以只能在发的那一刻同步读，不能缓存。
  - mock 里让目标开关可用的办法：把 `vrChatActive` 与 `vrChatOscAvailable` 置真，真实按下 VRC 按钮即可保持打开。
  - 撤回的镜像还差三样：收尾帧（上屏/取消时把 ChatBox 清掉或补发最后一次）、去重过期（VRChat 清空后同样的文本要能重发）、发送失败与重复实例的显式处理。
