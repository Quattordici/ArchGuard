# ARCH909: No archie.json found

The Archie package is referenced but no `archie.json` was found above the project, so nothing is checked. Add one next to your `.sln`, or set
`<ArchieConfig>`. Not reported for test projects. Info by default; it has no source location, so its severity can only be changed in a
global analyzer config (`is_global = true`), not in a `[*.cs]` section of `.editorconfig`.
