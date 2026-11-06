# IngameIME 宿主行为验证（2026-11-06）

接续 [26-11-06-6](./26-11-06-6_ingameime.md)。这一轮把「薄 C 接口 + .NET 探针」搭起来，逐条对照真实文本控件的输入法行为，确认新宿主是否忠实。代码全部是临时探针，未入库。

## 结论

- 新宿主忠实：下面「行为对照」表里每一行，真实控件与 IngameIME 宿主结果一致。
- 前一轮提到的两个「问题」（段内提交丢字、销毁上下文污染下一轮）**都不成立**，是探针自身的观测错误，见「已撤销的结论」。
- Rime 的 `inline_preedit` 必须保持开启，否则组字串只剩一个空格。
- 静态链接与动态链接都验证可行；动态方式的构建选项与限制见文末。

## 行为对照

真实控件指普通 Win32 `EDIT`（线程内调 `ITfThreadMgr::Activate`）；宿主指 `IngameIME::CreateInputContextWin32(..., API::TextServiceFramework, uiLess=true)`，`uiLess` 让应用自己画 UI，候选才走回调。

| 按键（先打 `nihc`） | 真实控件上屏                | IngameIME 宿主回调       |
| ------------------- | --------------------------- | ------------------------ |
| 空格                | 你好                        | commit 你好              |
| 数字键 2            | 第二候选直接上屏，无需空格  | commit 第二候选          |
| 下 下 空格          | 第三候选                    | commit 第三候选          |
| 回车                | 原始编码 `nihc`             | commit `nihc`            |
| 左 回车             | 只提交光标前的 `ni`         | commit `ni`              |
| Shift               | 原始编码 `nihc`             | commit `nihc`            |
| Esc                 | 无                          | PreEdit End，无 commit   |
| 左 空格 空格        | 你好                        | commit 你好              |
| PageDown 空格 空格  | 末页无下一页时同样得到 你好 | commit 你好              |
| Backspace 空格      | 你好                        | commit 你好              |
| 空格 逗号           | 你好，                      | commit 你好、commit 逗号 |

补充：

- 候选翻页：`PageDown` 与 `=` 都能翻页；候选多于一页时（如 `yi`）第二、三页内容正确，选中索引回到 0。
- 组字串：`PreEditContext.content` 是 UTF-8，`selStart`/`selEnd` 是上下文里的绝对偏移；非 BMP 候选（emoji）无损。
- 组字串会随状态退化：光标移进组字串时（左方向键、Tab），`content` 变成原始编码 `nihc`；选中前缀后变成混合态 `你hao`。这是 Rime 自己给的文本，不是宿主改写。
- 喂键节奏：每键 300ms、120ms、80ms 各 3 轮全部成功，节奏不是风险点。
- 输入法模式回调：会触发，但与中英切换不严格对应（中文态也报 `AlphaNumeric`），插件不能靠它判断中英。

## 段内提交的确切语义

librime 的 `Context::Commit()` 只做两件事：`commit_notifier_` 与 `Clear()`，提交文本来自 `composition_.GetCommitText()`。所以「选中前缀」不是立刻交给应用，而是把那段文本固定进组字串，组字结束才整体提交。按键语义按上表：空格固定前缀并继续组字，回车只把光标前那段交给应用。

## 已撤销的结论

- 「段内提交丢字」：不成立。当时看到「只有组字文本更新、没有 commit」，是因为组字还没结束，探针收尾又按了 Esc 把它取消；补按一次空格即整体提交。
- 「销毁上下文污染下一轮」：不成立。当时的异常是操作者本人在探针抢前台期间打字，字符被送进了组字。
- 教训：探针收尾不要发送 Esc 取消活着的组字，否则自己破坏观测；测试期间必须独占键盘。

## inline_preedit 实验

把 `weasel.custom.yaml` 的 `style/inline_preedit` 改为 `false` 并重新部署后：

- 候选照旧（每次按键一页 6 条，`ITfCandidateListUIElement` 路径不变）。
- 组字串只剩一个空格（`content` 是一个空格），拿不到编码与光标。
- 提交不受影响。

所以插件必须要求 `inline_preedit: true`，并把这条写进运行要求。

## 构建与依赖

- 临时探针与薄壳未入库，`temp/` 已被忽略。
- 动态方式需要的构建选项：`-DCMAKE_WINDOWS_EXPORT_ALL_SYMBOLS=ON`、`-DCMAKE_MSVC_RUNTIME_LIBRARY=MultiThreadedDLL`。上游默认把运行库写死为静态 `/MT`，必须覆盖，否则两个 DLL 各带一份 CRT 堆，`std::string` 跨 DLL 释放会崩。
- `InputContext::Version` 是静态数据成员，上述导出机制不导出它，薄壳不能引用。
- 动态方式产物：薄壳约 18 KB，`IngameIME_Shared.dll` 约 119 KB，两者都要随插件分发。

## 下一步

1. 以 submodule 引入 `IngameIME_Win32`（含其 `IngameIME-Common` 子模块），加构建脚本产出 `IngameIME_Shared.dll` 与薄壳。
2. 写 ADR：宿主改为 IngameIME，动态链接以满足 LGPL 2.1。
3. 插件集成：用事件替代隐藏 RichEdit 的读数路径；失焦与关页时取消组字。
4. VR 端到端验收：数字键选词、翻页、回车、英文直通。
