# XSOverlay-Keyboard-IME AGENTS.md

## 项目结构

```text
XSOverlayKeyboardIme/              BepInEx 5 插件
  KeyboardImePlugin.cs             入口；绑定配置、给 XSOverlay 打补丁、把宿主接到页面上
  MainThreadDispatcher.cs          任意线程 → Unity 主线程
  Game/                            一切碰 XSOverlay 的东西；Patches/ 里是 Harmony 补丁
  Chatbox/                         实时 OSC 广播状态与限频，不含 XSOverlay 类型
  Takeover/                        判定键盘页的按键归谁；纯数据，不含 XSOverlay 类型
  Ime/                             组字宿主：STA 线程、隐藏窗口、输入法上下文、喂键、输入法的语言栏按钮
  Page/                            注入页面脚本、写回已提交的文本、显示组字串与切换图标
  Input/                           虚拟键码
  Interop/                         原始声明：Win32、GDI、TSF 接口（输入法名字、语言栏）、shim 的 P/Invoke
  Diagnostics/                     日志
XSOverlayKeyboardIme.Tests/        单元测试；直接编译不依赖 XSOverlay 的源码
native/                            IngameIME.dll 的 CMake 构建
  IngameIME_Win32/                 子模块；上游的 TSF 实现
  IngameIME-Common/                子模块；上游的公共头文件
  shim/shim.cpp                    插件调用的 C ABI；LGPL，编进那个库
web/                               键盘页脚本；TypeScript 加一份样式表，打包后注入
Directory.Build.props              共享编译设置
Directory.Build.targets            可选的部署目标
docs/                              公开文档目录
  adr/                             一个决定一个文件，按编号
  archived/                        历史资料
    handoffs/                      历史交接
    notes/                         已结束的主题调查
  ROADMAP.md                       临时的路线图笔记，v1 完成后删除
CONTEXT.md                         领域词汇表
temp/                              本地草稿与探针工程；已被 git 忽略
```

## 命令

```bash
# 取一次上游源码，然后构建原生库
git submodule update --init
pwsh native/build.ps1

# 只构建
dotnet build -c Release

# 构建，并把插件与原生库复制进 XSOverlay
dotnet build -c Release -p:DeployToGame=true

# 跑单元测试
dotnet test XSOverlayKeyboardIme.Tests

# 格式化 C# 与 msbuild XML
dotnet csharpier format .

# 校验 C# 与 msbuild XML 的格式，不写文件
dotnet csharpier check .

# 校验 markdown 与结构化数据的格式，不写文件
node web/node_modules/prettier/bin/prettier.cjs --check "**/*.{md,json,yaml,yml,toml,css}"

# 对解决方案跑 C# 分析器，不写文件
dotnet format analyzers --verify-no-changes --severity info

# 页面脚本的类型检查、lint、打包
pnpm --dir web typecheck
pnpm --dir web lint
pnpm --dir web build

# 页面浏览器回归；首次运行前安装 Chromium
pnpm --dir web exec playwright install chromium
pnpm --dir web test
```

- 先构建页面脚本，再构建插件：`pnpm --dir web build` 写出 `web/dist/keyboard-ime.js`，插件把它嵌进去，缺了它插件拒绝构建。
- 先构建原生库，再部署：插件从自己所在目录加载 `IngameIME.dll`，缺了它就不给 XSOverlay 打补丁。
- 不要递归子模块：`IngameIME_Win32` 经 SSH 地址取它自己那份 `IngameIME-Common`，而 `native/CMakeLists.txt` 用的是同级子模块。
- CSharpier 是锁版本的本地工具。克隆后跑一次 `dotnet tool restore`，改动 `.config/dotnet-tools.json` 之后也要再跑一次。
- XSOverlay 路径默认是 `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`，用 `-p:GameDir=<path>` 覆盖。
- 回车与 ChatBox 浏览器回归复用安装版按键、音效、composer 与 view 模块，宿主答复仍为 mock，不覆盖真实宿主往返。路径用 `XSOVERLAY_GAME_DIR` 覆盖；缺少模块时跳过依赖它们的用例。
- 页面模块的真实 HTTP 路径以 `/apps/UserInterface/` 开头，回车测试服务器对旧路径与未知模块返回 404。
- 日志落到 XSOverlay 目录下的 `BepInEx\LogOutput.log`。
- CI 跑单元测试、页面浏览器回归、CSharpier、Prettier、测试工程上的分析器、页面脚本检查与原生构建。它从不构建插件：那需要 XSOverlay 的 Managed 程序集，而它们只留在这台机器上。

## 运行时约束

这些来自对真实 XSOverlay 进程的探测。破掉一条的代价是一次崩溃，或者白扔一天。

- XSOverlay 是 Mono x64 上的 Unity 2022.3.62。`TargetFramework` 保持在 `netstandard2.1` 或更高；低于它 Unity 程序集会报 `CS1705`。
- 引用 XSOverlay 自己的 `Managed/*.dll`。别的 Unity 版本的 stub 包对不上真实的 API 面。
- 游戏会销毁加载器对象，不能依赖插件自身的 `Update`；主线程工作沿 `UpdatePumpPatch` 的游戏帧入口执行。
- 没有备份和写明的恢复路径，不要改 XSOverlay 的 `StreamingAssets`。
- 输入法的中英状态归用户：绝不从代码切它，也绝不读模式。按输入法是否组字来分流按键。见 ADR 0026。
- 修改 ChatBox 广播或正式发送衔接前读 [0052](docs/adr/0052-broadcast-chatbox-previews-and-keep-native-submission.md)。
- 修改宿主前台寿命或 composer 失焦路径前读 [0050](docs/adr/0050-hold-the-host-focus-for-the-composer.md)，其规则已接受，实现与原型验证尚未完成。
- XSOverlay 即便 Steam 没有提权也是提权运行，所以普通 shell 停不掉它，往运行中的 XSOverlay 上部署会撞上被锁住的 `IngameIME.dll`。改为从 Steam 客户端的库页面停 XSOverlay：Steam 客户端服务以 `LocalSystem` 运行，会替 Steam 结束 XSOverlay。
- 经 Steam 重启 XSOverlay：`steam.exe -applaunch 1173510`。进程死掉之后 Steam 仍会报应用在运行，这时候早启动会失败并提示 “the game is already running”。等 `HKCU\Software\Valve\Steam\Apps\1173510` 的 `Running` 报成 0，再启动。

## 规则

### 推荐 Skill

「规则」一节引入了几个 Skill。缺哪个，就请用户从 GitHub 仓库 `mattpocock/skills` 安装。推荐用 npm 上的 `skills` 工具。

### 文档规则

- `CONTEXT.md` 是领域词汇表：规范术语加一句话定义，别的都不放。
- `docs/adr/` 一个决定一个文件，按编号；决定难反转、缺了理由会显得随意时，就添一个。
- 每个决定都掂量一下值不值得写 ADR，并且动手前问用户：说清选择、备选方案和你的 ADR 判断，然后等回答。没有东西可以悄悄定下。
- 两者都遵循 `domain-modeling` Skill。

- `AGENTS.md` 只承载约束与指针；ADR 已经记下的论证留在 ADR 里。
- 当前实现以源码、测试、已接受的 ADR 和 `docs/ROADMAP.md` 为准。
- `docs/archived/` 保存历史交接与调查，仅在追查旧证据时读取；其中的方案、状态和待办可能已失效。

- 除非用户明确要求，不要改旧的 ADR（front-matter 除外）。
- 每个 ADR 最顶上、标题之上先写 YAML front-matter：`status`（例如 `accepted` | `proposed` | `deprecated` | `superseded`），再加可选的关系键 `supersedes` / `superseded_by` 与 `amends` / `amended_by`，每个键都放带引号的 id（`['0001']`，绝不裸写 `0001`，YAML 会把它读成八进制）。
- 关系是双向的：一条关系是一对互相镜像的键。只是提到另一个 ADR 不配拥有键；只有文件自己就是建立关系的那一方时，ADR 才记这个键。

### 代码风格规则

- 通用能力先查标准 API、现有依赖与运行时自带库，再选成熟库；已有可用实现时，不手写序列化、转义、编码等通用逻辑。

- 一种文件类型只配一个格式化器。`CSharpier` 管 C# 与 msbuild XML（`*.cs`、`*.csproj`、`*.props`、`*.targets`、`*.slnx`）；`prettier` 管 markdown、结构化数据与页面样式表（`*.{md,json,yaml,toml,css}`）。
- 绝不裸跑 `dotnet format`，它会回退 CSharpier 的改动。它在这里的唯一角色是 `dotnet format analyzers`。
- Prettier 与某条 ESLint 规则冲突时，关掉那条规则，保留 Prettier 的改动。

- 文档字符串保持简短：只讲要点，别的没有。ADR 已经论证过的东西不在代码里重复——改为链接 ADR。
- `Takeover/` 保持不含 XSOverlay 类型：测试工程直接编译它，混进一个 XSOverlay 或 Unity 类型就毁掉整个测试构建。

### 放行规则

输入法说了算。键盘做它没有插件时也会做的事：键进去，输入法拿它怎么处理，就是用户看到的答案。只用公共 API（例如 TSF，IME），不做按输入法的特例。

修改模式项来源前读 [0042](docs/adr/0042-hide-the-switch-without-an-icon.md) 与 [0051](docs/adr/0051-use-taskbar-uia-for-shell-mode-clicks.md)：公开 TSF 优先，缺项时允许 InputSwitch 原图与任务栏 UIA 点击。Shell ABI 的历史适配证据见[归档记录](docs/archived/notes/input-mode-shell.md)。

- 先按来源给缺陷分类：插件、XSOverlay、输入法，还是系统。说出来源，以及把它归到那里的证据。
- 由输入法或系统产生的结果是预期行为：切换时被丢下的组字、组字结束后才到的提交、布局打不出的字符、把编码当原文提交。记为基线，别去动它。
- 只改插件自己造成的缺陷。先确认来源；没有来源的报告还只是个问题。
- 兜底只覆盖插件自己造成的失败，而且注释里要点名那个失败。说不出名字的兜底就是缺陷本身。
- 只有在直路被证明走不通之后，才动用等待、重试、定时器或合成按键；要求它的 ADR 记录那份证明。
- 删掉机制，而不是给它加条件：为了一个情况而添的第二个分支，就是删掉这个机制的信号。

## Commit

Use English conventional commit messages:

```text
type(optional scope): description

- List of change descriptions, focus one point per row

Optional footer(s)
```
