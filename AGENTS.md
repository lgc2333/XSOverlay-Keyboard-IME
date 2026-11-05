# XSOverlay-Keyboard-IME AGENTS.md

## Project Structure

```text
XSOverlayKeyboardIme/              BepInEx 5 plugin; the only shipped artifact
  KeyboardImePlugin.cs             entry point; binds config, patches the game, starts the host
  Host/                            STA thread, hidden window, foreground lease, key feeding
  Takeover/                        decides which keyboard page keys belong to the plugin
  Game/                            reads the game's keyboard overlay state
  Page/                            injects the page script, writes committed text back, shows the composition
  Composition/                     composition state, view and inline-preedit watch; pure logic
  Candidates/                      reads the candidate page the IME publishes
  Sinks/                           ITfUIElementSink and the UI element event queue
  Interop/                         TSF interfaces, Win32 P/Invoke, IMM text reader
  SelfCheck/                       diagnostic self-check; only in a build with -p:SelfCheck=true
  Diagnostics/                     logging
XSOverlayKeyboardIme.Tests/        unit tests; compile the dependency-free sources directly
web/                               keyboard page script; TypeScript, bundled for injection
Directory.Build.props              shared compile settings
Directory.Build.targets            optional deploy target
docs/adr/                          one file per decision, numbered
docs/handoffs/                     dated session handoffs, one file per session
CONTEXT.md                         domain glossary
temp/                              local scratch and probe projects; git-ignored
```

## Commands

```bash
# build only
dotnet build -c Release

# build and copy the plugin into the game
dotnet build -c Release -p:DeployToGame=true

# diagnostic build that also compiles the self-check
dotnet build -c Release -p:SelfCheck=true

# run unit tests
dotnet test XSOverlayKeyboardIme.Tests

# format C# and msbuild XML
dotnet csharpier format .

# verify C# and msbuild XML formatting without writing
dotnet csharpier check .

# verify markdown and structured data formatting without writing
node web/node_modules/prettier/bin/prettier.cjs --check "**/*.{md,json,yaml,yml,toml}"

# run the C# analyzers over the solution without writing
dotnet format analyzers --verify-no-changes --severity info

# Page script check, lint, build
pnpm --dir web typecheck
pnpm --dir web lint
pnpm --dir web build
```

- Build the page script before the plugin: `pnpm --dir web build` writes `web/dist/keyboard-ime.js`, which the plugin embeds and refuses to build without.
- Never ship a self-check build. The self-check is behind `#if IME_SELF_CHECK`; add code for it only under `SelfCheck/`.
- CSharpier is a pinned local tool. Run `dotnet tool restore` once after cloning, and after any change to `.config/dotnet-tools.json`.
- The game path defaults to `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`. Override it with `-p:GameDir=<path>`.
- Logs land in the game folder at `BepInEx\LogOutput.log`.

## Runtime constraints

These come from probing the live game process. Breaking one costs a crash or a wasted day.

- The game runs Unity 2022.3.62 on Mono x64. Keep `TargetFramework` at `netstandard2.1` or higher; Unity assemblies fail `CS1705` below it.
- Reference the game's own `Managed/*.dll`. Stub packages from other Unity versions do not match the real API surface.
- Do not edit the game's `StreamingAssets` without a backup and a documented restore path.
- Restart the game through Steam: `steam.exe -applaunch 1173510`. After the processes die, Steam keeps reporting the app as running, and an early launch fails with "the game is already running". Wait until `HKCU\Software\Valve\Steam\Apps\1173510` reports `Running` as 0, then launch.

## Rules

### Recommend Skills

The Rules section introduced some skills. If some skill is missing, please ask user to install it from Github repo `mattpocock/skills`. Recommend using `skills` tool from npm.

### Docs Rules

- `CONTEXT.md` is the domain glossary: canonical terms and one-line definitions, nothing else.
- `docs/adr/` holds one file per decision, numbered; add one when a decision is hard to reverse and would look arbitrary without the reason.
- Weigh every decision for whether it deserves an ADR, and ask the user before taking it: state the choice, the alternatives, and your ADR verdict, then wait for the answer. Nothing gets decided silently.
- Follow the `domain-modeling` skill for both.

- `docs/handoffs/` records what one session discussed at that time. It is archived history, not current truth: a handoff may be out of date, and a later ADR or a later document overrides it.

- Do not modify old ADRs (excluding their front-matter) unless user explicitly asks.
- Every ADR opens with YAML front-matter at the very top, above the title: `status` (e.g. `accepted` | `proposed` | `deprecated` | `superseded`), then the relation keys `supersedes` / `superseded_by` and `amends` / `amended_by`, each holding quoted ids (`['0001']`, never a bare `0001`, which YAML reads as octal).
- Relations are bidirectional: a relation is a mirrored pair of keys. A plain mention of another ADR earns no key, and an ADR records a relation only when its own file is the one making it.

### Code Flavor Rules

- One formatter per file type. `CSharpier` formats C# and msbuild XML (`*.cs`, `*.csproj`, `*.props`, `*.targets`, `*.slnx`); `prettier` formats markdown and structured data (`*.{md,json,yaml,toml}`).
- Never run `dotnet format` barely, it would revert CSharpier's change. Its only role here is `dotnet format analyzers`.
- If Prettier conflicts with an ESLint rule, turn off that rule, keep Prettier's change.

- Docstrings stay short: the key point, nothing else. What an ADR already reasons through is not repeated in code — link the ADR instead.

## Commit

Use English conventional commit messages:

```text
type(optional scope): description

- List of change descriptions, focus one point per row

Optional footer(s)
```
