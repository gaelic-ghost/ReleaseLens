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

## Project layout

- `src/ReleaseLens`: executable and testable core modules
- `tests/ReleaseLens.Tests`: behavior tests
- `examples`: documented example input

## Intentional limits

The first milestone does not include Git history discovery, hosted integrations, configuration files, persistence, plugins, AI classification, or NuGet packaging.
