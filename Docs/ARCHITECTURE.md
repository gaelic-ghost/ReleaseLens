# Architecture

## Classification

The initial ReleaseLens architecture is a durable building-block change: one F# executable project containing explicit domain, parsing, assessment, rendering, and CLI modules, plus one F# xUnit test project.

This shape keeps the useful core pure and testable while avoiding a speculative library package. A library extraction becomes justified when a second host, public API, or package consumer needs the same assessment pipeline.

## Data flow

1. The CLI reads a JSON release document from a file or standard input.
2. The parser validates required fields and maps category text into explicit F# domain cases.
3. The assessment pipeline assigns an explainable score to each change and derives an overall risk level.
4. A renderer produces deterministic Markdown or JSON.
5. `Program.fs` owns input/output operations and exit codes.

Side effects remain at the edges. The parser, assessment, and rendering modules accept values and return values.

`Program.run` receives explicit standard-input, standard-output, and standard-error streams. The executable entry point supplies the process console streams, while tests supply in-memory streams to verify content and exit codes without global console mutation.

## Domain model

- `ChangeCategory` is a discriminated union for breaking changes, dependency changes, migrations, security fixes, ordinary fixes, and unknown input.
- `ReleaseChange`, `ReleaseInput`, `AssessedChange`, and `RiskReport` are records that make each transformation boundary explicit.
- `RiskLevel` is a discriminated union derived only from the aggregate score.

The parser treats missing or unrecognized categories as domain data instead of discarding them or failing the entire release. Structural input problems—invalid JSON, duplicate property names, missing required values, wrong JSON types, and duplicate IDs after whitespace trimming—return descriptive validation errors.

## Determinism

Assessment has no clock, environment, network, or random input. Both renderers preserve input change order. JSON properties and category summaries are written in a fixed order, and both output formats use line-feed newlines with one final newline.

Markdown escapes structure-changing characters and flattens inline line breaks. File output uses UTF-8 without a byte-order mark and create-new semantics, so an existing destination is never overwritten.

## Project layout

- `src/ReleaseLens`: executable and testable core modules
- `tests/ReleaseLens.Tests`: behavior tests
- `examples`: documented example input

## Intentional limits

The first milestone does not include Git history discovery, hosted integrations, configuration files, persistence, plugins, AI classification, or NuGet packaging.
