# ReleaseLens

ReleaseLens is a small F# CLI that reads structured release changes and produces an explainable release-risk report.

The first milestone focuses on a deterministic path from JSON input to Markdown or JSON output. It is intentionally one executable project plus one test project: the domain logic stays directly testable without introducing a library boundary before another host or consumer exists.

## Status

Initial implementation in progress.

## Requirements

- .NET 10 SDK

The repository pins the .NET 10 SDK family through `global.json` while allowing newer .NET 10 feature bands and patches.

## License

ReleaseLens is available under the Apache License 2.0.
