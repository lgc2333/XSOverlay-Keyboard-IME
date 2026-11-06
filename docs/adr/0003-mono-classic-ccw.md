---
status: superseded
superseded_by: ['0024']
---

# Mono 上使用经典 CCW，不使用 ComWrappers

Unity 2022.3 的 Mono BCL 既没有 ComWrappers 也没有 UnmanagedCallersOnly，基于 ComWrappers 的写法在此编译不过。实测经典 CCW（ComVisible 类加 GetComInterfaceForObject）能正常接收 TSF 回调，因此 COM 回调一律走经典 CCW，手写 vtable 只作为后备。

## Considered Options

- ComWrappers：在 Mono 上不可用。
- 手写 vtable：可用，但只为承载 IUnknown 而多写一层，代码量更大。
- 经典 CCW：采纳。

## Consequences

- 回调对象必须全程保活，且在回调里禁止让异常穿越 COM 边界。
