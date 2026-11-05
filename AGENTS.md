# XSOverlay-Keyboard-IME AGENTS.md

## Project Structure

```text
src/XSOverlayKeyboardIme/   BepInEx 5 plugin; the only shipped artifact
Directory.Build.props       shared target framework, game references, BepInEx packages
Directory.Build.targets     optional deploy target
docs/adr/                   one file per decision, numbered
docs/handoffs/              dated session handoffs, one file per session
CONTEXT.md                  domain glossary
temp/                       local scratch and probe projects; git-ignored
```

## Commands

```bash
# build only
dotnet build -c Release

# build and copy the plugin into the game
dotnet build -c Release -p:DeployToGame=true
```

- The game path defaults to `D:\SteamLibrary\steamapps\common\XSOverlay_Beta`. Override it with `-p:GameDir=<path>`.
- Logs land in the game folder at `BepInEx\LogOutput.log`.

## Runtime constraints

These come from probing the live game process. Breaking one costs a crash or a wasted day.

- The game runs Unity 2022.3.62 on Mono x64. Keep `TargetFramework` at `netstandard2.1` or higher; Unity assemblies fail `CS1705` below it.
- Reference the game's own `Managed/*.dll`. Stub packages from other Unity versions do not match the real API surface.
- Do not edit the game's `StreamingAssets` without a backup and a documented restore path.

## Rules

### Recommend Skills

The Rules section introduced some skills. If some skill is missing, please ask user to install it from Github repo `mattpocock/skills`. Recommend using `skills` tool from npm.

### Docs Rules

- `CONTEXT.md` is the domain glossary: canonical terms and one-line definitions, nothing else.
- `docs/adr/` holds one file per decision, numbered; add one when a decision is hard to reverse and would look arbitrary without the reason.
- Weigh every decision for whether it deserves an ADR, and ask the user before taking it: state the choice, the alternatives, and your ADR verdict, then wait for the answer. Nothing gets decided silently.
- Follow the `domain-modeling` skill for both.

- Do not modify old ADRs (excluding their front-matter) unless user explicitly asks.
- Every ADR opens with YAML front-matter at the very top, above the title: `status` (e.g. `accepted` | `proposed` | `deprecated` | `superseded`), then the relation keys `supersedes` / `superseded_by` and `amends` / `amended_by`, each holding quoted ids (`['0001']`, never a bare `0001`, which YAML reads as octal).
- Relations are bidirectional: a relation is a mirrored pair of keys. A plain mention of another ADR earns no key, and an ADR records a relation only when its own file is the one making it.

### Code Flavor Rules

- Format documentation (`*.md`) and structured data (`*.{json,yaml,toml}`, etc.) with `prettier` when it is available.

- Docstrings stay short: the key point, nothing else. What an ADR already reasons through is not repeated in code — link the ADR instead.

## Commit

Use English conventional commit messages:

```text
type(optional scope): description

- List of change descriptions, focus one point per row

Optional footer(s)
```
