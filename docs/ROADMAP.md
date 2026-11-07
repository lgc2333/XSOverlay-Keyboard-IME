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
- [x] 切换键与图像提取成功与否分开判断，切换输入项时清除旧图，失焦或明确缺项时隐藏并恢复空格。见 [0042](./adr/0042-hide-the-switch-without-an-icon.md)。
- [x] 删除启动等待、合成 Alt、前台重试、切换后等待与下拉框忙碌提示，前台切换失败时停止本次送键。名字与图标随通知或后续按键刷新，保留用户操作触发的取消与页面重建的 60 毫秒等待。见 [0041](./adr/0041-no-recovery-for-native-ime-results.md)。

## 当前待解决

- [x] 按语言栏附加图标的有无显示切换键，公开 TSF 模式项优先，缺项时读取 Shell 原图或字形。原图不可读时使用占位，美式布局隐藏，点击不可用时保持 disabled。见 [0042](./adr/0042-hide-the-switch-without-an-icon.md)。
- [ ] Shell 原生点击尚未证明能在归还前台前可靠完成，当前仅显示状态；系统 DLL 适配也仅覆盖精确验证的版本，详见[适配说明](./input-mode-shell.md)。

- [ ] 微软中文与日语输入法在输入框为焦点时打不出任何字，不管是中文还是英文模式，打不出一个字，没有预编辑，没有候选，没有一个字上屏输入框。英文模式也如此。
- [ ] 微软日语的快捷键（jis布局键盘）貌似不生效（变换，非变换，平假片假快捷切换）按平片假切换只能出来一个预编辑的f，按非变换只能按出来一个预编辑的a。尚不清楚是不是xsoverlay原有问题。
- [ ] 在输入框非焦状态下按alt+shift+f在vscode中不生效。只会打出一个大写F。尚不清楚是不是xsoverlay原有问题。
- [ ] 实现有问题。回车在输入框为焦点的时候没有任何反应。这个回车在组字时应该原样传给输入法。不组字时应该维持原行为，上屏输入框，相当于点击发送键。原始问题：组字中按回车会出问题，输入框直接丢焦，当前组字不会上屏输入框
- [ ] 当前已激活输入法会排在列表第一位。始终不要重排输入法列表。尽量维持系统原有排序。在我的系统下是中文：小狼毫，微软拼音；英文：美式键盘；日语：微软输入法
- [ ] 中英文切换按钮在 JIS 布局下出现，违反原先的 ADR 设计决策。这可能是某个 Agent 的擅自决定，如果有记录新 ADR，需废除

## 待真机验证

暂无

## 下一步

- [ ] 优化 VRC ChatBox 体验
  - [ ] ChatBox 目标按钮打开后，组字串实时送进 ChatBox。**实现过、验证过、已撤回**：页面侧先按 800ms 节流发过 `{"text":"nihc"}`，但它与 [0011](./adr/0011-input-only-no-autosend.md)「插件不复制发送职责」冲突；更硬的阻碍是**清空不了**——XSOverlay 的 OSC 服务对空白文本直接拒绝，组字结束后半成品会留在 ChatBox 里。建议 v1 不做。
  - 输入框获得焦点时向 VRChat 上报正在输入，失焦时取消。路线：XSOverlay 的 API 没暴露 OSC typing，但插件可以在 C# 侧直接向 VRChat 的 OSC 输入端口发 `/chatbox/typing` 的 UDP 包；前提是用户开了 VRChat 的 OSC。同样要先想清楚它算不算「第二份发送职责」。
  - 已知接口：页面发 `SendVRChatChatboxText`，载荷 `{"text": "..."}`，上限 144 字符；要求 VRChat 在线且 OSC 可用（`vrChatActive` / `vrChatOscAvailable`）。「正在输入」对应 VRChat 的 OSC typing 参数，XSOverlay 的 API 目前没有暴露，需要另找入口或放弃。
  - 实现路线（已实测）：注入脚本 `import('/Shared/js/api.js')` 能拿到 `Send` 与 `Commands.SendVRChatChatboxText`，页面自己的 `keyboard.js` 用的是同一个 URL，模块按 URL 缓存，所以是同一份实例、同一条连接、同一个 overlay 身份；手动 `Send` 已经能从页面的 mock 服务器收到。
  - 目标开关：状态在 `.keyboard-vrc-toggle` 的 `aria-pressed` 上，但页面会按自己的会话状态重写这个属性，所以只能在发的那一刻同步读，不能缓存。
  - mock 里让目标开关可用的办法：把 `vrChatActive` 与 `vrChatOscAvailable` 置真，真实按下 VRC 按钮即可保持打开。
  - 撤回的镜像还差三样：收尾帧（上屏/取消时把 ChatBox 清掉或补发最后一次）、去重过期（VRChat 清空后同样的文本要能重发）、发送失败与重复实例的显式处理。
