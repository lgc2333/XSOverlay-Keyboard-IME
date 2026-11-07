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
- [x] 按键归属只有一张表：页面、输入法、系统、吞掉。Sticky 的 Shift 与 Toggle 的 Caps 留在页面，页面据此决定字母的大小写；其他 Win 组合吞掉，Win+空格 交给系统。见 [0036](./adr/0036-one-table-for-key-ownership.md)、[0038](./adr/0038-latched-modifiers-stay-with-the-page.md)。
- [x] Win+空格 交给系统，按下即切系统输入法；名字与图标随后刷新，新输入法没有语言栏项时退回页面自带字形。代价是它切的是整个桌面的语言，VR 里看不见任务栏。见 [0036](./adr/0036-one-table-for-key-ownership.md)。

## 待真机验证

每条：怎么做 → 应该看到什么。

- [ ] 聚焦不抖：点输入框、再点别处，来回几次 → 键盘行宽度不变，右 Ctrl 不被顶出，切换键只在聚焦后出现，且只有一个。
- [ ] 组字中射线点输入框：打 `nihc`，射线点同一个输入框 → 组字被打断，只剩原本的文字，不留半截拼音。
- [ ] 组字中点清空：打 `nihc`，点清空 → 输入框清空，组字结束，旧文字不回填。
- [ ] 组字中打大写：打 `nihc`，按 Shift 再打字母；按 Caps 打字母，再按 Caps 打字母 → Shift 管一个字母，Caps 锁上再解开。
- [ ] 组字中按中英切换键：打 `nihc` 不选词，按空格左侧的切换键 → 组字按输入法自己的规矩收掉，之后打的字母是英文。插件不替输入法上屏；Rime 有时不收组字，那是它自己的毛病。
- [ ] 组字中 Win+空格：打 `nihc`，按 Win+空格；再快速连按两次 → 组字收掉，输入法名字与图标跟着换。
- [ ] 候选栏滚动：打一个候选很多的词，PageUp/PageDown 翻到最后 → 活跃候选始终完整可见，不被渐隐挡住。
- [ ] 下拉框：点名字旁的下拉框，选另一个输入法 → 当前项带勾与描边，点一条即切，点外面收起。
- [ ] 提交晚于组字结束：连续快打、回车提交、取消后立刻重打 → 不丢字。
- [ ] ChatBox 发送：打开 VRC 目标按钮，打一句中文发送 → 文本进 VRChat 的 ChatBox，插件不自动发送。

下一轮 VR 的顺序：点输入框 → 按空格左侧切换键切到中文 → 逐条走上面这十条 → 顺手看候选栏下拉框与 ChatBox 发送按钮。对应的日志行：`feed Escape`（取消）、`feed Win+Space` 与 `the IME is now`（切换）、`feed Return`（Win+空格 或下拉框切换前上屏）、`key refused … style=Sticky/Toggle`（粘滞键是否被正确放行）、`the page switched to profile`（下拉框切换）。

## 待实现

- [ ] 当输入法没有语言栏图标时（如英文键盘），隐藏中英切换键。现在这种情况下切换键退回页面自带的字形，仍然可点，但没有对应项时它按下去什么也不做。

## 下一步

- [ ] 优化 VRC ChatBox 体验
  - [ ] ChatBox 目标按钮打开后，组字串实时送进 ChatBox。**实现过、验证过、已撤回**：页面侧先按 800ms 节流发过 `{"text":"nihc"}`，但它与 [0011](./adr/0011-input-only-no-autosend.md)「插件不复制发送职责」冲突；更硬的阻碍是**清空不了**——游戏的 OSC 服务对空白文本直接拒绝，组字结束后半成品会留在 ChatBox 里。建议 v1 不做。
  - 输入框获得焦点时向 VRChat 上报正在输入，失焦时取消。路线：游戏的 API 没暴露 OSC typing，但插件可以在 C# 侧直接向 VRChat 的 OSC 输入端口发 `/chatbox/typing` 的 UDP 包；前提是用户开了 VRChat 的 OSC。同样要先想清楚它算不算「第二份发送职责」。
  - 已知接口：页面发 `SendVRChatChatboxText`，载荷 `{"text": "..."}`，上限 144 字符；要求 VRChat 在线且 OSC 可用（`vrChatActive` / `vrChatOscAvailable`）。「正在输入」对应 VRChat 的 OSC typing 参数，游戏的 API 目前没有暴露，需要另找入口或放弃。
  - 实现路线（已实测）：注入脚本 `import('/Shared/js/api.js')` 能拿到 `Send` 与 `Commands.SendVRChatChatboxText`，页面自己的 `keyboard.js` 用的是同一个 URL，模块按 URL 缓存，所以是同一份实例、同一条连接、同一个 overlay 身份；手动 `Send` 已经能从页面的 mock 服务器收到。
  - 目标开关：状态在 `.keyboard-vrc-toggle` 的 `aria-pressed` 上，但页面会按自己的会话状态重写这个属性，所以只能在发的那一刻同步读，不能缓存。
  - mock 里让目标开关可用的办法：把 `vrChatActive` 与 `vrChatOscAvailable` 置真，真实按下 VRC 按钮即可保持打开。
  - 撤回的镜像还差三样：收尾帧（上屏/取消时把 ChatBox 清掉或补发最后一次）、去重过期（VRChat 清空后同样的文本要能重发）、发送失败与重复实例的显式处理。
