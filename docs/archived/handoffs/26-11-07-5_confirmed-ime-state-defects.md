# 输入法状态与焦点问题交接（2026-11-07）

本文件交接下拉列表、清空、输入法切换与抢焦的调查结果，均已定位，尚未修复。回车、切换键布局与候选栏横轴的交接见 [上一份交接](./26-11-07-4_confirmed-vr-defects.md)，当前状态见 [路线图](../../ROADMAP.md)。用户要求不要 commit。

## 基线与证据范围

- 源码 HEAD 为 `8b6003d`。工作区有并行修改，接手前检查差异，保留已有改动与暂存状态。
- 真机安装版为 `ce68c56`，主插件 SHA-256 前缀为 `F1BC91535166ACE4`，本轮未替换主插件。
- 本轮经用户授权重启 XSOverlay，加载只读诊断插件，记录页面事件、原生组字回调、前台交接与 TSF 活动档案。诊断不发送按键、不切换档案、不读写中英模式。
- 第一版诊断的 COM 创建在 Unity Mono 中失败，其活动档案字段无效。第二版通过 `CoCreateInstance` 调用 `GetActiveProfile`，已在 Unity Mono 验证有效，不使用默认档案回退。下文切换结论均来自第二版。
- 下文序号用于说明同次采集的事件顺序，证据已在本文件展开。清空与切换来自两次独立采集，序号不能跨采集排序。

## 下拉列表：过滤遗漏与模型缺项

**来源：插件。** [ImeProfiles](../../../XSOverlayKeyboardIme/Ime/ImeProfiles.cs) 的 `ReadLanguageProfiles()` 只检查描述非空，没有检查用户是否启用该档案。

同机只读探针确认：

- 微软五笔已注册，但 `IsEnabledLanguageProfile=0`，仍被插件列出。
- 英语 `0409` 的旧 `EnumLanguageProfiles` 返回零项。
- 新 `ITfInputProcessorProfileMgr.EnumProfiles(0409)` 返回 enabled 的美式普通键盘布局，类型为 `2`，HKL 为 `04090409`。
- 新枚举同时包含非键盘文本服务，不能仅按 enabled 过滤，还需键盘类别。

[LanguageProfile](../../../XSOverlayKeyboardIme/Ime/LanguageProfile.cs) 只有 TIP 的服务与档案标识，没有普通键盘布局的类型和 HKL；旧激活路径也只处理 TIP。修复需同时覆盖枚举、过滤、模型和激活，避免列表中出现无法切换的条目。[0039](../../adr/0039-switch-imes-from-the-bar.md) 的既有范围需在选定修复方案时核对。

验收：列表与用户启用的中文小狼毫、微软拼音、英语美式键盘、日语微软输入法一致，不显示未启用的五笔或非键盘文本服务，每项都能实际输入。

## 清空：取消丢失与旧组字回放

**来源：插件。** 存在两个独立缺陷。

从空输入框开始组字时，[showPreedit](../../../web/src/composer.ts) 只写 `.value`，未同步原页工具栏，清空按钮保持禁用。用户已在 VR 确认。

已有内容时，清空采集的序号 `652–658` 确认：

1. 清空按钮可用，输入框长度为 8。
2. 清空执行后长度为 0，页面失活，但 DOM 短暂仍将 composer 视为活动元素。
3. 插件收到 `composer-inactive`，刷新状态。
4. [KeyboardPage.Attach](../../../XSOverlayKeyboardIme/Page/KeyboardPage.cs) 将缓存的 `_view` 再次发送到页面。
5. `showPreedit()` 在指针抬起前恢复旧文本、预期值和预期选区，长度重新为 8。
6. 本次没有取消通知，也没有 Escape，原生组字仍继续。

[checkFocus](../../../web/src/composer.ts) 只检查 DOM 焦点，没有识别整页失活。旧视图又把外部清空留下的差异抹掉，后续检查无法发现清空。按真实顺序重放当前页面模块：保留回放时取消次数为 0，旧编码恢复；去掉回放时取消次数为 1，输入框保持清空。

用户进一步确认：已有 `你好`，输入 `ni` 后清空，仍能继续组字；空格提交没有写回，输入框退回 `你好`。取消丢失让原生组字继续，提交又受页面焦点校验阻挡，结束视图恢复了旧的基础文本。不能将此归为输入法拒绝取消，因为失败时根本没有送出 Escape。

验收：空框与已有内容两种情况均可清空；清空后组字和候选结束，旧文本不回填，下一键开始新组字。保留 [0027](../../adr/0027-cancel-composition-on-caret-move.md) 和 [0034](../../adr/0034-a-rebuilt-composer-is-not-a-blur.md) 对真正失焦与布局重建的区分。

## 输入法切换：后台激活失效与通知缺口

**来源：插件的激活上下文和刷新时序。** 当前系统 `IsLegacySwitchingMode=False`，使用共享输入法选择。

下拉采集序号 `517–529`，以及后续多次重复，均为：

| 阶段                        | 宿主真实活动档案 |
| --------------------------- | ---------------- |
| 后台执行 `SwitchProfile` 后 | 微软拼音         |
| `ForegroundLease.Take` 前   | 微软拼音         |
| 处理 `WM_SETFOCUS` 时       | 微软拼音         |
| `Take` 返回前               | 小狼毫           |
| 随后 `KeyFeeder.Send`       | 小狼毫           |

HKL 始终为 `08040804`。因此下拉确实激活成功，所选档案在取得前台时被替换，普通按键尚未发送。这是真实回退，不是名字误报。[SwitchProfile](../../../XSOverlayKeyboardIme/Ime/ImeHost.cs) 在后台调用旧 `ActivateLanguageProfile`，下一键才取得前台，这个顺序无法保持选择。

Win+空格采集序号 `1320–1335` 确认另一个时序：

1. 系统组合键成功发送，`KeyFeeder.Send` 返回时活动档案仍为小狼毫。
2. 插件刷新显示并归还前台。
3. 下一键的 `Take` 前，活动档案已为微软拼音，证明系统稍后完成了切换。
4. 下一键取得前台时又回到小狼毫。

期间 HKL 不变，没有 `WM_INPUTLANGCHANGE`。[KeyFeeder](../../../XSOverlayKeyboardIme/Ime/KeyFeeder.cs) 只排空键盘消息；[ImeHost](../../../XSOverlayKeyboardIme/Ime/ImeHost.cs) 随即读取状态；[HostWindow](../../../XSOverlayKeyboardIme/Ime/HostWindow.cs) 仅通过语言变更消息通知宿主，没有 TSF 档案激活事件的接线。同语言档案稍后切换时，页面得不到更新。用户观察到名字与中文图标不变、字母原样写入，重聚焦后显示小狼毫英文状态，与此一致。

本轮还有前台获取失败的样本：后台档案保持微软拼音，但字母没有发送；空格仍由原页处理。用户中途给桌面窗口聚焦后，现象才变成回退。排查时需先区分未送键与送键前档案回退。

[ReadName](../../../XSOverlayKeyboardIme/Ime/ImeProfiles.cs) 另有默认档案回退，单看名字不足以证明真实活动档案。本轮已用无回退的 getter 排除这一歧义。

修复 API 的作用范围与前台顺序尚未选择。[ActivateProfile 官方文档](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfinputprocessorprofilemgr-activateprofile) 说明新 API 支持普通键盘布局、进程和桌面作用范围；桌面范围不是已批准的选择。实现前按 AGENTS.md 讨论方案与 ADR，保留 [0041](../../adr/0041-no-recovery-for-native-ime-results.md) 的边界，不用等待或重试掩盖失败。

验收：下拉与 Win+空格之后的下一键仍属于所选输入法；同语言和跨语言切换都更新真实名称与图标。重新激活后的默认中英状态归输入法，插件不读写模式。

## 抢焦：历史焦点资格未撤销

**来源：插件。** [reclaimFocus](../../../web/src/composer.ts) 记住最后聚焦过的输入框，真正失焦后没有撤销资格；旧节点断开后直接对新输入框调用 `.focus()`。

隔离真实页面已复现失焦后的布局重建抢焦。真机补测序号 `1796–1811` 又确认：composer 失焦并报告 inactive；在 composer 没有焦点时操作 Win+空格；没有点击输入框，随后出现 `focusin`、`dom-replaced` 与 `composer-active`。用户确认桌面浮窗输入被抢。

XSOverlay 的 `InputManager.UpdateKeyboardState()` 每 100 毫秒检查前台线程 HKL，变化后发出 `EventKeyboardFormatChanged`，API 推送 `UpdateKeyboardLayout`。原页 `keyboard.js` 的 `OnUpdateKeyboardLayout()` 清除旧布局并重建输入框，触发上述历史焦点恢复。当前 `UpdateKeyboardState` 页面消息只更新状态与样式，不会重建输入框；不要沿用旧文档把两者混为一谈的描述。

验收：composer 真正失焦后，从桌面切换到美式或日语布局不会抢焦；composer 仍持有焦点时，正常布局重建可保留焦点和组字。

## 接手边界

以上结论来自真实 VR、公开 TSF 查询、安装版程序集与页面的只读检查，以及隔离事件重放。它们证明缺陷与失败顺序，尚未验证修复。临时诊断增加时序开销，验收应在撤除诊断后再做。遵守 [AGENTS.md](../../../AGENTS.md)，保留工作区差异，不提交。
