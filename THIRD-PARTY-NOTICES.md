# Third-party notices

The plugin ships one native library, `IngameIME.dll`. It contains IngameIME, the
sources under `native/shim/`, and everything needed to reach them from the plugin. The
whole file is licensed under the GNU Lesser General Public License, version 2.1. The
plugin's own code is licensed under the MIT licence; see [LICENSE](LICENSE).

The licence text ships in this repository at [native/LICENSE-LGPL-2.1.txt](native/LICENSE-LGPL-2.1.txt).

## IngameIME_Win32

- Source: <https://github.com/lgc2333/IngameIME_Win32>
- Upstream: <https://github.com/Windmill-City/IngameIME_Win32>
- Pinned commit: `a95d660c1d5b2ba0b0e933266df4e23e5f6c7232`
- Fork base: `0e77cac6d2cf1e6a69155dda9d8090ca2f6852b7` (tag `v6.1.5`)
- Licence: LGPL 2.1
- Contributes the TSF input context, the composition and candidate callbacks, and the
  report of the input mode.
- The fork fixes COM interface identity and the reference count returned during object destruction.

## IngameIME-Common

- Source: <https://github.com/Windmill-City/IngameIME-Common>
- Pinned commit: `b930e47dcb169a54eb2ee374e88a84b239c4e062`
- Licence: LGPL 2.1
- Contributes the public header the library is built from.

## How to replace the library

`IngameIME.dll` is a separate file beside the plugin, so you may rebuild and replace
it. The sources are the two submodules under `native/`; `native/build.ps1` rebuilds
the file. See [ADR 0023](docs/adr/0023-dynamic-link-shim.md).
