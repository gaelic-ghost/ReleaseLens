# AGENTS.md

## Purpose

ReleaseLens is a small F# command-line application that turns structured release changes into deterministic, explainable risk reports.

## Architecture

- Keep domain types and transformations in the `ReleaseLens` executable project until a second real host or package consumer requires a library boundary.
- Keep parsing, assessment, and rendering as separate modules with explicit inputs and outputs.
- Keep file, standard-input, standard-output, and process-exit side effects in `Program.fs`.
- Use records for named product data and discriminated unions for closed domain alternatives.
- Preserve F# compile order explicitly in each `.fsproj`.
- Do not introduce service, repository, manager, dependency-injection, or persistence layers without an immediate use case and Gale's approval.

## Operator Experience

- Every CLI error must identify the failed operation, the affected input or output when known, and a plausible corrective action.
- Keep output deterministic so identical input produces byte-for-byte identical Markdown and JSON.
- Preserve the documented input and output vocabulary unless a deliberate format version change is approved.

## Validation

Run these commands serially:

```sh
dotnet restore --locked-mode
dotnet tool restore
dotnet fantomas --check src tests
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Do not publish a NuGet package from this repository.
