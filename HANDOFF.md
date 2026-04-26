# VSFilterText — Handoff / Resume Notes

Self-contained snapshot of project state and the open debugging thread, written so a fresh Claude Code session can continue without prior context.

---

## What this is

Visual Studio extension that opens a **read-only filtered side-by-side view** of the active document.

- User presses `Ctrl+Alt+F` on an open document.
- A new tab opens in the same tab group, titled `Foo.cs [Filtered]`, with a query textbox at the top.
- Typing a substring hides non-matching lines (true hiding via `IElisionBuffer`, not dimming/outlining).
- Double-click a line in the filter tab → jump back to the source document at that line.
- Filter tab auto-refreshes when the source is edited.
- Closing the source closes the filter tab; closing the filter tab leaves the source untouched.

**Target VS:** Visual Studio 18 (2026), Windows, amd64.
**Branch:** `claude/vs-extension-scaffold-iNHtC` (push here only).
**Repo:** `aaronlieberman/vsfiltertext` (GitHub MCP scope is restricted to this repo).

---

## Locked-in design decisions

1. **Pure Classic VSSDK** in a single `.vsix`. No hybrid with the new (out-of-proc) Extensibility API — the new API can't host an editor surface or projection buffer, and the Classic-only command piece is trivial via `.vsct`.
2. **One filter doc per source doc.** Re-pressing `Ctrl+Alt+F` on a source that already has a filter tab focuses the existing one (delegated to `IVsUIShellOpenDocument.OpenSpecificEditor` dedup).
3. **No scroll sync** — only the double-click jump-to-source navigation.
4. **Auto-update on source edits** — projection tracks edits natively; we just re-evaluate which lines match.
5. **Tab title format:** `Foo.cs [Filtered]`.
6. **Read-only via view role** (omits `PredefinedTextViewRoles.Editable`) and via `IVsPersistDocData.IsDocDataDirty` always returning `0`. (We *removed* the belt-and-suspenders `CreateReadOnlyRegion` — see "Recent fixes" below.)
7. **Substring match** in v1 (ordinal case-insensitive). Regex/case/invert/whole-word are extension points behind `FilterPredicate.IsMatch`.

---

## File layout

```
VSFilterText.sln
src/VSFilterText/
  VSFilterText.csproj                 # SDK-style, net472, VSSDK 17.14.*
  source.extension.vsixmanifest       # InstallationTarget [18.0,19.0)
  VSFilterTextPackage.cs              # AsyncPackage, registers factory + command + RDT listener
  Commands/
    OpenFilterViewCommand.cs          # Ctrl+Alt+F handler
    VSFilterTextPackage.vsct          # Command + keybinding
  Editor/
    FilterEditorFactory.cs            # IVsEditorFactory for vsfiltertext:// monikers
    FilterDocument.cs                 # Composes elision buffer + view + state + engine
    FilterDocumentPane.cs             # IVsWindowPane + IVsPersistDocData (never dirty)
    FilterQueryMargin.cs / .xaml      # Top WPF margin: textbox + match count
    FilterQueryMarginProvider.cs      # MEF; restricted by FilterView role
    FilterQueryMarginViewModel.cs     # 100ms DispatcherTimer debounce
    NavigateToSourceCommandHandler.cs # IWpfTextViewCreationListener; double-click → source
    FilterDocumentLifetime.cs         # IVsRunningDocTableEvents; closes filter on source close
    FilterViewRoles.cs                # const "VSFilterText.FilterView"
    FilterDocumentKeys.cs             # property bag keys (e.g. SourceMoniker)
  Filter/
    FilterPredicate.cs                # IsMatch — sole place filter semantics live
    FilterEngine.cs                   # ExpandSpans + ElideSpans on filter / source change
  State/
    FilterState.cs                    # Text, MatchCount, Changed event
HANDOFF.md                            # this file
README.md                             # do not overwrite
```

---

## Architecture summary

```
Source ITextBuffer (untouched)
    │
    ├──► IElisionBuffer (projection over source)
    │         │   ExpandSpans/ElideSpans on filter change or source edit
    │         │
    │         └──► IWpfTextView (role set without Editable)
    │                  │
    │                  ├──► FilterQueryMargin (top: textbox + match count)
    │                  └──► PreviewMouseLeftButtonDown (ClickCount==2) → source nav
    │
    └──► IVsRunningDocumentTable events → FilterDocumentLifetime
              │
              └──► closes filter frame when source closes
```

- Moniker scheme: `vsfiltertext://<UrlEncoded(sourcePath)>`.
- `FilterEditorFactory` claims this scheme, resolves the source `ITextBuffer` via `IVsRunningDocumentTable.FindAndLockDocument` → `IVsTextLines` → `IVsEditorAdaptersFactoryService.GetDocumentBuffer`, then constructs a `FilterDocument` and `FilterDocumentPane`.
- `FilterDocument` stashes `FilterState`, `FilterEngine`, and the source moniker on the view's `Properties` bag so MEF-discovered parts (margin provider, double-click handler) can find them by view role.
- `FilterDocumentLifetime` caches `IVsWindowFrame` references via `OnBeforeDocumentWindowShow` and on source close calls `filterFrame.CloseFrame(FRAMECLOSE_NoSave)` (the RDT does not have a direct `CloseDocuments` API in this surface).

---

## Build pipeline

Use `Microsoft.VSSDK.BuildTools 18.5.*` with `VSSDKBuildToolsAutoSetup=true`. This is the VS 2026-era package and it auto-imports `Microsoft.VsSDK.targets` and wires `CreateVsixContainer` into the build. No manual `<Import>` or custom AfterBuild targets are needed.

Earlier sessions used `17.14.*` (VS 2022-era) and required a large set of workaround targets. All of that is gone. See `BUILD.md` for build instructions.

---

## Recent fixes (latest session)

- **`HwndSource` leak in `FilterDocumentPane`** — the WPF host's `HwndSource` was created but never stored or disposed. Now stored in a field and disposed in `Dispose()`.
- **Removed `CreateReadOnlyRegion` from `FilterDocument`** — it covered `Span(0, initialLength)` of the elision buffer, which is reshaped by every `ExpandSpans`/`ElideSpans` call. The view role (no `Editable`) is the correct, sufficient guard against typing; the static region was a potential interference source for projection edits.

These landed in commit `d8f00ef` on top of `e96a84b`.

---

## Build status

**VSIX builds cleanly.** `bin\Debug\net472\VSFilterText.vsix` is produced by a plain `msbuild /t:Rebuild`. See `BUILD.md`.

Contents: `VSFilterText.dll`, `VSFilterText.pkgdef`, `extension.vsixmanifest`, standard VSIX boilerplate.

## Next: smoke test in VS 2026 Experimental Instance

Install via F5 from VS 2026 (or double-click the `.vsix`), then:

1. Open any text file.
2. `Ctrl+Alt+F` → filter tab opens with `<filename> [Filtered]` and focused query textbox.
3. Type a substring → non-matching lines hide; match count updates.
4. Double-click a line → source tab activates, caret jumps to that line.
5. Edit source → filter tab auto-refreshes.
6. Close source tab → filter tab closes automatically.
7. Re-press `Ctrl+Alt+F` on a source that has a filter → focuses the existing filter (no duplicate).
8. Save All / Ctrl+S → filter tab does not appear dirty and does not prompt.
9. Two source docs → two independent filter docs.

If the extension doesn't load, check `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_<hash>Exp\ActivityLog.xml` for editor-factory registration errors.

---

## Known `// NEEDS VERIFICATION:` markers in code

These are tagged in source where we used a likely-correct VSSDK API shape that we couldn't confirm against installed reference assemblies. Resolve at first successful Windows build:

- `FilterEngine.Apply` — exact `IElisionBuffer.ElideSpans` / `ExpandSpans` argument shape (we use `NormalizedSpanCollection` in source-buffer coordinates).
- `NavigateToSourceCommandHandler` — uses a direct WPF `PreviewMouseLeftButtonDown` + `ClickCount==2` instead of the modern `ICommandHandler` path. Acceptable for a read-only view with no competing handler.
- `FilterEditorFactory` — minimal `IVsEditorFactory` registration; some custom editors require additional `IVsEditorFactoryNotify` plumbing.
- `FilterDocumentPane` — direct `IVsWindowPane` (not the managed `WindowPane` helper). Simpler for a read-only prototype; revisit if command routing turns out to be needed.

---

## How F5 is wired

A VSIX project is a class library and won't run directly. The csproj has:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <StartAction>Program</StartAction>
  <StartProgram>$(DevEnvDir)devenv.exe</StartProgram>
  <StartArguments>/rootsuffix Exp</StartArguments>
</PropertyGroup>
```

`$(DevEnvDir)` is set by MSBuild when running inside VS; `/rootsuffix Exp` selects the isolated Experimental hive at `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_<hash>Exp`.

---

## Quick re-orientation for the next session

1. Read this file.
2. Read `BUILD.md` for how to build.
3. Read `src/VSFilterText/VSFilterText.csproj` — now clean and simple.
4. Read `src/VSFilterText/source.extension.vsixmanifest` — confirms `[18.0,19.0)` installation target.
5. Skim `Editor/FilterDocument.cs`, `Editor/FilterEditorFactory.cs`, `Filter/FilterEngine.cs` — the three pieces most likely to need debugging once the smoke test runs.
