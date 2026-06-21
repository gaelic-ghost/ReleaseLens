# ReleaseLens

ReleaseLens is a small F# CLI that reads structured release changes and produces an explainable release-risk report.

It demonstrates explicit domain modeling with discriminated unions and records, pattern matching, pure transformation pipelines, descriptive validation errors, and deterministic Markdown and JSON output.

## What it does

ReleaseLens accepts a JSON release document, maps every change into a known or unknown category, assigns a visible category weight, caps the aggregate score at 100, and derives one of four risk levels:

| Score | Risk |
| ---: | --- |
| 0–9 | Low |
| 10–29 | Moderate |
| 30–59 | High |
| 60–100 | Critical |

The initial category weights are deliberately simple:

| Category | Score | Why it matters |
| --- | ---: | --- |
| Breaking change | 40 | Consumers may need compatibility work. |
| Security fix | 25 | Rollout timing and regression review are unusually important. |
| Migration | 20 | Deployment may require coordinated state or data changes. |
| Dependency change | 15 | Transitive behavior or deployment compatibility may change. |
| Unknown | 10 | An unclassified change needs manual review. |
| Ordinary fix | 2 | Every fix carries a small baseline regression risk. |

ReleaseLens does not pretend this is a universal risk model. Its value is that the model is deterministic, inspectable, and easy to revise.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

The repository targets .NET 10 LTS and pins the .NET 10 SDK family through `global.json`, allowing newer .NET 10 feature bands and patches.

## Run it

Generate a Markdown report:

```sh
dotnet run --project src/ReleaseLens -- examples/release.json
```

Generate JSON:

```sh
dotnet run --project src/ReleaseLens -- examples/release.json --format json
```

Read JSON from standard input:

```sh
cat examples/release.json | dotnet run --project src/ReleaseLens -- -
```

Write a report to a file:

```sh
dotnet run --project src/ReleaseLens -- examples/release.json --output release-risk.md
```

Use `--help` for the complete CLI syntax.

## Input format

The input is one JSON object with a non-empty `release` string and a `changes` array:

```json
{
  "release": "2.0.0",
  "changes": [
    {
      "id": "a12bc34",
      "summary": "Remove the legacy authentication endpoint",
      "category": "breaking",
      "details": "Consumers must migrate to the token exchange endpoint."
    }
  ]
}
```

Each change requires a unique, non-empty `id` and `summary`. `category` and `details` are optional. Categories are case-insensitive and accept:

- `breaking`
- `dependency`
- `migration`
- `security`
- `fix`

A missing or unrecognized category remains in the report as `unknown`; ReleaseLens preserves the original category text in JSON output so the result stays explainable.

See [`examples/release.json`](examples/release.json) for a complete input document.

## Output

Markdown is the default human-readable output. It includes the overall risk, aggregate score, category counts, and a per-change explanation.

JSON output uses a fixed property and collection order. Identical input produces byte-for-byte identical output, including its final newline.

## Develop

Run the repository checks serially:

```sh
dotnet restore
dotnet tool restore
dotnet fantomas --check src tests
dotnet build --no-restore
dotnet test --no-build
```

The solution contains one executable project and one xUnit test project. The executable owns the domain modules because no second host or reusable package consumer exists yet. See [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) for the boundary decision.

## Next milestones

Sensible follow-on slices are:

1. Add a versioned input schema and machine-readable schema validation.
2. Add configurable scoring profiles without changing the default deterministic model.
3. Add adapters for conventional-commit or changelog extraction while preserving the current structured input as the core boundary.

NuGet publishing is intentionally out of scope.

## License

ReleaseLens is available under the [Apache License 2.0](LICENSE).
