# Asset Doctor

Experimental s&box editor diagnostics for quoted asset paths in the active project's physical `Assets` directory.

## What it checks

- Missing project or editor-known package asset references
- Unsafe and non-portable path syntax
- Case mismatches
- Circular direct references among scanned current-project text assets
- Unreadable or oversized source text assets
- Direct native Asset Browser references/dependants through right-click actions

## Important limitation

The scanner is heuristic: it reads quoted values in supported text formats. It is **not** a complete dependency graph, publish-manifest validator, or blocking quality gate. Review findings before acting on them.

## Reports

Exports unique timestamped Markdown, text, and JSON reports. JSON includes `detectionMode` so CI consumers cannot mistake the output for exhaustive dependency coverage.
