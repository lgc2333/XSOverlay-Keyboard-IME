# TSF 排队提交与上下文收尾调查（2026-11-08）

接续[六项输入问题调查](./26-11-08-1_roadmap-input-defects.md)，核对旧上下文回收是否同时保留迟到提交。当前状态见[落地方案](archived-notes/README.md)，本轮未选择新架构，未修改或部署产品代码。

## 实验范围

本机 Windows `10.0.26200.0`，`msctf.dll` 为 `10.0.26100.9949`，MSVC x64，COM STA。三组共 48 个有效独立子进程，每个案例重复三次，全部正常退出。

前两组使用空白 TSF 上下文与自有编辑请求，不读写文本，并确认请求已排队但尚未执行。第三组通过独立客户身份合成固定 `a`、`b`，使用现有原生库和公开 C ABI；只确认 End 已到且 commit 为零，没有直接观察原生请求的排队返回值。回调观察状态独立保活，时限只用于停止观察。

实验不发送系统按键，不选择输入法档案，不查询或切换输入法模式，不操作 XSOverlay。前台身份前后相同；第三组接收窗口隐藏且不可激活，其他两组不创建显式窗口。

第三组原生库对应 [fork 修复提交](https://github.com/lgc2333/IngameIME_Win32/commit/a95d660c1d5b2ba0b0e933266df4e23e5f6c7232)，SHA256 为 `6EBD10021C909D34CB3DE5A89CC513E24C2C139A90F0117F5214670E9A8507C5`。它包含已验证的 COM 修复，异步提交策略仍为原实现。

## 排队请求的正常收尾

第一组一个上下文，6 个案例各三次，所有请求为 `S_OK / TF_S_ASYNC`。

| 操作                                  | 执行与释放观察                                       |
| ------------------------------------- | ---------------------------------------------------- |
| 先泵消息，再清理                      | 请求在消息泵中执行一次，随后最后释放                 |
| `UnadviseSink`，再泵消息              | 解除通知不取消请求，消息泵仍执行一次                 |
| `Pop(TF_POPF_ALL)`                    | 请求在 `Pop` 返回前最后释放，没有执行                |
| 最后一次 `Deactivate`                 | 请求在 `Deactivate` 返回前重入执行一次，随后最后释放 |
| `Pop` → `Deactivate`                  | 请求在 `Pop` 中释放，没有执行                        |
| `UnadviseSink` → `Pop` → `Deactivate` | 同上                                                 |

第二组在同线程创建两个文档和上下文，并配对激活两次，6 个案例各三次。

| 操作                                        | 观察                                             |
| ------------------------------------------- | ------------------------------------------------ |
| 先泵消息，再清理                            | A、B 请求分别执行一次并释放                      |
| 非最后一次 `Deactivate`                     | 调用内未执行、未释放两个请求，随后消息泵才执行   |
| 连续两次 `Deactivate`，中间不泵消息         | 第一次没有回调，第二次在返回前执行并释放两个请求 |
| `Pop` A，再泵消息                           | A 请求在 `Pop` 内释放且不执行，B 请求仍正常执行  |
| `Pop` A，再进行非最后一次 `Deactivate`      | A 同上，B 保留至随后消息泵执行                   |
| A 请求运行时追加请求，预先排队 A 的异步标记 | 标记先执行，追加请求随后执行                     |

各案例的 `Pop`、`Deactivate` 均为 `S_OK`。A 移出栈后，其原上下文的 `GetStatus` 为 `TF_E_DISCONNECTED`，B 仍为 `S_OK`。A 文档的 `GetTop/GetBase` 还可返回其他上下文，不能据此称文档为空。

这些是本机合成路径的观察，不是 `Pop` 的通用取消保证。异步标记反例中的追加请求也是已知合成请求，没有真实输入法参与。

## 现有原生库的提交对照

第三组 4 个案例各三次。每次正常结束固定 `a` 的组字，确认原生 End 已到而 commit 仍为零，再执行对应操作。

| 操作                                        | 三次观察                                                          |
| ------------------------------------------- | ----------------------------------------------------------------- |
| 泵到提交并排空已知请求，再销毁              | 每次只交付一次 `a`                                                |
| 直接 `iime_destroy`，再泵消息               | 销毁内及其后约 1000 毫秒均未观察到 `a` 提交                       |
| `iime_set_activated(0)`，保留上下文并泵消息 | 停用返回时未提交，随后每次交付一次 `a`                            |
| A 待提交、B 保持 `b` 组字，销毁 A           | A 未交付；B 的 `b` 和一个活动组字保留，正常结束后独立交付一次 `b` |

销毁前释放了实验取得的上下文与组字接口，之后不再调用该对象，回调来源记录保活至进程退出。该对照证明“保留上下文等待已知提交”与“直接销毁”的结果不同，不证明任意销毁都内存安全或真实输入法的未来提交完整。

`iime_set_activated(0)` 会终止组字，本对照不能代替 [0050](../adr/0050-hold-the-host-focus-for-the-composer.md) 要求的默认窗口失焦。各项等待和异步标记也只覆盖已知合成请求。

## 公共契约与源码约束

[ITfEditSession](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itfeditsession) 明确允许请求不执行：授予锁前上下文销毁，或请求方文本服务先停用，`DoEditSession` 不会调用。因此请求资源应在析构中清理，不能只在执行回调中清理。

[RequestEditSession](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfcontext-requesteditsession) 保证待处理异步请求按接收顺序处理，同步请求优先，不覆盖标记后追加或未来才提出的请求。[CreateContext](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfdocumentmgr-createcontext) 规定，销毁上下文或将其移出栈时释放传入宿主对象的引用，移出栈与销毁上下文并不等价。

已查阅的 [Deactivate](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfthreadmgr-deactivate)、[Pop](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfdocumentmgr-pop) 和 [UnadviseSink](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfsource-unadvisesink) 文档没有建立未来 IME 结果的完成屏障。这是本次证据范围，不是所有公共接口均无此能力的证明。

当前[原生提交处理器](../../native/IngameIME_Win32/src/TfCompositionHandler.cpp) 把自身作为异步 session，执行时通过裸 `inputCtx` 取得上下文并调用提交回调。[原生析构](../../native/IngameIME_Win32/src/TfInputContextImpl.cpp) 依次停用、解除通知、移出上下文和停用线程，没有本地待处理请求记录。COM 引用能保活处理器，不能自动保活其父对象；本轮正常退出不能代替该项验证。

## 候选事件的线程作用域

另有 6 个独立进程使用自有 `ITfUIElement`，两种情况各重复三次。同 STA、同线程管理器的两个监听器均收到 Begin、Update、End；两个独立 STA 各自创建管理器时，各自只收到本线程发出的事件，对方事件为零。创建、通知、解除订阅和停用均为 `S_OK`，回调都在所属线程执行。该组不创建窗口，不产生真实候选。

[ITfUIElementMgr](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nn-msctf-itfuielementmgr) 由线程管理器提供。[BeginUIElement](https://learn.microsoft.com/en-us/windows/win32/api/msctf/nf-msctf-itfuielementsink-beginuielement) 允许应用用 `pbShow=TRUE` 保留输入法自己的 UI，用 `FALSE` 隐藏它。本轮只写 `TRUE`，没有验证多个监听器对显示值的组合规则。

该对照支持继续调查独立 STA 的事件隔离，不能证明真实输入法候选、模式保持、焦点转移或资源回收。单 STA 集中监听与独立 STA 仍都是候选，未知文档归属的处理尚未决定。

## 独立 STA 的档案继承

后续三个独立进程用公开 TSF 接口创建 A、B 两个 STA，均使用 `ActivateEx(TF_TMAE_UIELEMENTENABLEDONLY)`。初始真实档案均为小狼毫，A 依次请求进程范围激活微软拼音、US、微软日语，每次再创建 C。各线程自行创建、读取并释放 COM 接口，没有窗口、原生输入上下文、送键或模式查询。

| A 的选择 | A 的真实项 | 已有 B 的真实项 | 后建 C 的初始真实项 | C 显式选择后 |
| -------- | ---------- | --------------- | ------------------- | ------------ |
| 微软拼音 | 微软拼音   | 微软拼音        | 小狼毫              | 微软拼音     |
| US       | US         | 微软拼音        | 小狼毫              | US           |
| 微软日语 | 微软日语   | 微软日语        | 小狼毫              | 微软日语     |

三次结果一致，选择与读取均为 `S_OK`，身份按完整类型、语言、服务、档案和布局比较。创建、关闭 C 后 A、B 的相应状态保持；C 显式选择 US 后，B 仍为微软拼音。末尾显式恢复并确认 A、B 为初始小狼毫，前台 HWND 前后相同，正常退出。

这仅反驳无焦点初始化时的自动继承假设，不证明窗口真正获焦后的档案或模式。独立 STA 方案仍须核对每个线程的真实活动项，不能把一次进程范围请求当作同步完成，也不能把显式重选档案当作模式保持的证明。

## 对后续调查的约束

不能按组字结束、一次异步标记或一次 `Deactivate` 决定回收，也不能把正常退出当作提交已完整交付。释放内存安全、已知请求完成、未来输入法结果完整，是三个独立问题；本轮不解释当前单上下文的微软无输入症状。

迟到提交、普通字符与候选仍需分别证明来源；未知候选归属、模式保持、有界回收、真实 Vuplex 后台撤销及物理编辑键尚未闭合。JIS 原码与首次前台失败仍需现场采样，既有被动诊断已准备且尚未部署。本轮没有新增 ADR。
