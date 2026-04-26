# Building VSFilterText

## Prerequisites

- Visual Studio 2026 Community (or Pro/Enterprise), fully installed
- No separate .NET SDK or MSBuild install needed — use the VS 2026 Developer Command Prompt

## Build from the command line

Open **VS 2026 Developer Command Prompt** (Start → "Developer Command Prompt for VS 2026"), then:

```cmd
cd E:\VSFilterText
msbuild src\VSFilterText\VSFilterText.csproj /t:Restore
msbuild src\VSFilterText\VSFilterText.csproj /t:Rebuild /p:Configuration=Debug
```

Output: `src\VSFilterText\bin\Debug\net472\VSFilterText.vsix`

## Build from PowerShell / Claude Code (no interactive prompt)

The VS 2026 developer environment can be sourced inline:

```powershell
$vsDevCmd = "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\Tools\VsDevCmd.bat"
$proj = "E:\VSFilterText\src\VSFilterText\VSFilterText.csproj"

# Restore
cmd /c "`"$vsDevCmd`" -arch=amd64 && msbuild `"$proj`" /t:Restore /v:minimal"

# Build
cmd /c "`"$vsDevCmd`" -arch=amd64 && msbuild `"$proj`" /t:Rebuild /v:minimal /p:Configuration=Debug"
```

The `vswhere.exe not found` warning from `VsDevCmd.bat` is benign — the prompt initialises correctly regardless.

## Build from inside VS 2026

Open `VSFilterText.sln`, set configuration to Debug, press **Ctrl+Shift+B**.  
F5 launches a second VS instance under the Experimental hive (`/rootsuffix Exp`) with the extension loaded.

## What the VSIX contains

| File | Purpose |
|------|---------|
| `VSFilterText.dll` | Extension assembly |
| `VSFilterText.pkgdef` | Package registry entries (generated from attributes) |
| `extension.vsixmanifest` | VSIX metadata and installation targets |
| `[Content_Types].xml`, `manifest.json`, `catalog.json` | VSIX packaging boilerplate |

## Key build facts

- Uses `Microsoft.VSSDK.BuildTools 18.5.*` with `VSSDKBuildToolsAutoSetup=true`.  
  This auto-imports `Microsoft.VsSDK.targets` and wires `CreateVsixContainer` into the build — no manual `<Import>` or custom AfterBuild targets needed.
- `Microsoft.VisualStudio.SDK 17.14.*` provides the API assemblies; 17.14.x is still the current stable API surface for VS 18.
- The VSIX targets VS 2026 only (`[18.0, 19.0)` in `source.extension.vsixmanifest`).
