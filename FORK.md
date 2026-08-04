# About this fork

This is an unofficial fork of [mukunku/ParquetViewer](https://github.com/mukunku/ParquetViewer),
maintained for personal use. Upstream is the original project and deserves the credit; everything
here is built on top of their work.

## What this fork changes

Bug fixes on top of upstream:

- **The audio player now renders.** `OnDataBindingComplete` assigned `column.CellTemplate`, but a
  `DataGridView` only consults the template when it creates a cell, and data binding has already
  created every cell by then. The assignment had no effect and audio columns showed raw bytes.
  Upstream still has this, including in v4.2.0.
- **Filter queries and CSV exports are culture invariant.** `DataView.RowFilter` always expects `.`
  as the decimal separator, so on a comma-decimal locale a float became `1,5`, and inside an
  `IN (...)` clause the comma silently changed which rows matched.
- **The list read path is no longer quadratic.** Reading 20k rows of list data went from ~19s to
  ~50ms. `ListValueBuilder.ReadRows` re-enumerated the whole column for every row.
- **Column sizing.** Sizing stopped entirely at the first audio column, and the decimal width cap
  leaked into every column after it, making widths depend on column order.
- **Image column detection** samples several values instead of only the first, so one non-image blob
  no longer makes a whole column unclickable.
- **Resource leaks**: a font allocated per painted NULL cell, a form handler never detached,
  undisposed bitmaps and context menus. Plus a race between the compare and the redraw in progress
  reporting.

Features not in upstream:

- **Cell preview panel** below the grid showing the selected cell's full value. The grid truncates
  at 2000 characters and has tooltips disabled, so long values could not be read at all.
- **Line break markers** in grid cells, since a cell draws a single line and multi-line values
  otherwise render as one run-on string. Glyph and colour are configurable.
- **Settings window** covering the new preferences alongside the existing ones.

Behaviour changes specific to this fork:

- **No network calls.** The update check queried upstream's releases API, which can only report this
  fork as out of date or ahead of it; analytics posted to Amplitude. Both are off behind a constant,
  with the implementations left in place. The consent prompt is suppressed too.
- **Diagnostics are logged** to `%LOCALAPPDATA%\ParquetViewer\parquetviewer.log` rather than
  silently swallowed. The file is dropped once it passes a megabyte.

## Versioning

Fork builds keep the upstream `major.minor.patch` they are based on and use a **revision of 100 or
above**, which upstream has never used:

| Upstream | This fork |
| --- | --- |
| `4.2.0.0` | `4.2.0.100`, `4.2.0.101`, ... |

That makes a fork build identifiable at a glance and means it can never claim a version number
upstream might later release. `SemanticVersion` in this codebase only parses numbers, so the
readable label lives in `AssemblyInformationalVersion` (`4.2.0.100+fork.PerikiyoXD`).

Tags use a `-fork` suffix (`v4.2.0.100-fork`) so they never collide with upstream's `vX.Y.Z.W`.

## Syncing with a new upstream release

**Rebase, don't merge.** Rebasing keeps the fork's commits as a clean stack on top of upstream, so
each sync is one linear replay and "what have we actually changed?" stays answerable. Merging
accumulates merge commits and makes that progressively harder.

```sh
git fetch upstream --tags

# Do the work on a branch so main is never left in a half-resolved state
git checkout -b integrate/upstream-vX.Y.Z main
git rebase upstream/main

# Resolve conflicts, then for each one:
#   git add <file> && git rebase --continue

# Verify before touching main
dotnet build src/ParquetViewer.sln -c Debug
dotnet test src/ParquetViewer.sln --no-build -c Debug
dotnet build src/ParquetViewer/ParquetViewer.csproj -c Release_SelfContained  # not compiled in Debug

git checkout main
git reset --hard integrate/upstream-vX.Y.Z
git push --force-with-lease origin main
```

Bump `AssemblyVersion` and `AssemblyInformationalVersion` in
`src/ParquetViewer/Properties/AssemblyInfo.cs` to the new upstream version with revision `100`, then
tag `vX.Y.Z.100-fork`.

### Where conflicts come from

Rebasing onto v4.2.0 produced 3 conflicts across 15 commits, and **all of them were in metadata**,
not code:

- `src/Directory.Packages.props` — both sides change package versions. Keep upstream's, re-add the
  `Snappier` pin (see below).
- `src/ParquetViewer/Properties/AssemblyInfo.cs` — both sides set the version. Take upstream's
  `major.minor.patch`, keep the fork's `.100` revision and the informational version.

Fork-only code lives in its own files, which is why it conflicted with nothing:

- `src/ParquetViewer/Controls/CellPreviewPanel.cs`
- `src/ParquetViewer/SettingsForm.cs`
- `src/ParquetViewer/MainForm.CellPreview.cs`

**Keep it that way.** Upstream cannot conflict with a file it does not have. When a change must
touch a shared file, prefer adding a method over rewriting an existing one.

The designer files (`*.Designer.cs`, `*.resx`) are the sharpest edge: they are generated, upstream
regenerates them, and an editor that rewrites the BOM or line endings turns a 20-line addition into
a whole-file diff. Check `git diff --numstat` on those files before committing.

## Local build notes

- `Snappier` is pinned to 1.3.1 for GHSA-pggp-6c3x-2xmx. It arrives transitively via `Parquet.Net`,
  and central package management only applies a `PackageVersion` to something actually referenced,
  so the pin needs **both** the version entry and a direct `PackageReference` in
  `ParquetViewer.Engine.ParquetNET`. Drop it once upstream's `Parquet.Net` pulls a fixed version.
- A local `nuget.config` is gitignored. Some machines inherit a broken package source and need one;
  that is a local concern, not a project one.
- Close the running app before building. It holds `ParquetViewer.exe` open, and the build then fails
  to copy the apphost — which can silently leave you testing a stale binary.
- The DuckDB fallback path is only compiled in `Release_SelfContained`, so a Debug build proves
  nothing about it. Build that configuration explicitly before releasing.
