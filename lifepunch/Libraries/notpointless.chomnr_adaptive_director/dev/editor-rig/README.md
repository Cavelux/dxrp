# Real s&box editor gate

This is adapted from the humanoid-retargeter autonomous pipeline. It creates a disposable minimal project, junctions this library into `Libraries/local.adaptive_director`, launches `sbox-dev.exe`, executes a core smoke test inside the editor, records JSON, and closes the editor.

Run: `powershell -ExecutionPolicy Bypass -File dev/editor-rig/run_editor_gate.ps1 -Clean`
