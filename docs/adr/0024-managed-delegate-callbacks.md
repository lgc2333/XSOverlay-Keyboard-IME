---
status: accepted
supersedes: ['0003']
superseded_by: []
amends: []
amended_by: []
---

# 原生回调改用托管委托

[0003](./0003-mono-classic-ccw.md) 的前提是托管侧要实现 COM 接口。改用 IngameIME 后，插件不再实现任何 COM 接口，原本的 COM 回调路径不复存在；但原生到托管的回调仍然存在，承载方式必须重新定。改为把 `cdecl` 委托的函数指针交给薄壳，委托在进程内静态保活。

## Considered Options

- 经典 CCW，见 [0003](./0003-mono-classic-ccw.md)：已经没有需要包装的 COM 接口；否决。
- `UnmanagedCallersOnly`：Unity 2022.3 的 Mono BCL 没有该特性，编译不过；否决。
- `Marshal.GetFunctionPointerForDelegate` 传委托指针：采纳。

## Consequences

- 项目里不再有 `ComInterfaceType` 的托管实现；`Sinks/` 与 `ITfUIElementSink` 一并消失。
- 委托必须静态保活到进程结束。一旦被回收，原生侧的回调会跳到已释放的 thunk。
- 回调里抛出的异常会以 `STATUS_FATAL_USER_CALLBACK_EXCEPTION` 直接终止进程，不是可捕获的普通异常。因此每个回调体一律自行 try/catch，绝不让异常穿出边界。
