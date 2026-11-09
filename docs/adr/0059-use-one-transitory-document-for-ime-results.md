---
status: accepted
amends: ['0022']
---

# 用单一 transitory 文档收取输入法结果

现有 IngameIME 结束请求会在异步执行时读取并删除当前全文；受控旧 End 后新组字的实际处理链已复现误收新文本，见[原生清理记录](../archived/notes/late-commit.md#原生侧来源身份)。改用一个 STA、一个组字宿主和一个 TSF transitory 文档，通过确定的 result 范围收取提交，消费结果时保留当前组字，删除旧 End 全文清理机制。

本决定修订 [0022](./0022-ingameime-host.md) 的原生存储与结果读取路径，候选、档案和语言栏继续使用公开 TSF 能力。不建立每目标文档，不恢复原目标追溯；交付仍遵循 [0057](./0057-apply-ime-results-to-the-current-composer.md)，前台寿命仍遵循 [0050](./0050-hold-the-host-focus-for-the-composer.md)。

备选是保留旧后端，增加独立结束请求、私有范围及隐式清理分类。选择 transitory 是为删除这些清理推断，并非旧后端已被证明不可修复。有限拼音、范围消费和释放证据不等于完整兼容；真实日语、候选、档案切换、默认失焦与回调内退休均须重新验收。实际销毁须等原生续行安全结束，丢弃结果不等于对象可释放。
