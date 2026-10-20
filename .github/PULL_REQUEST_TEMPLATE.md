## Summary

<!-- What does this change, and why? Link the related issue, e.g. "Closes #123". -->

## Type of change

- [ ] Bug fix
- [ ] New rule or diagnostic
- [ ] Refactoring / performance
- [ ] Documentation
- [ ] Build, CI or tooling

## Checklist

- [ ] `dotnet build -c Release` and `dotnet test -c Release` pass locally
- [ ] Tests pass on Roslyn 4.8 too (`dotnet test -c Release -p:TestAgainstFloors=true`)
- [ ] Tests added or updated where it makes sense
- [ ] New diagnostics are registered in `AnalyzerReleases.Unshipped.md`
- [ ] No new Roslyn API newer than 4.8 (the floor), and no new analyzer dependencies

## Notes for the reviewer

<!-- Anything that needs extra attention, e.g. changes to workflows, .csproj files or build/Archie.targets. -->
