module ReleaseLens.Tests

open System.Text.Json
open ReleaseLens
open Xunit

[<Fact>]
let ``parser maps documented categories into explicit domain cases`` () =
    let input =
        """
        {
          "release": "2.0.0",
          "changes": [
            {
              "id": "abc123",
              "summary": "Remove the legacy endpoint",
              "category": "breaking",
              "details": "Consumers must move to /v2."
            },
            {
              "id": "def456",
              "summary": "Refresh the HTTP client",
              "category": "dependency"
            }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Error errors -> failwith $"Expected valid input, received: {errors}"
    | Ok parsed ->
        Assert.Equal("2.0.0", parsed.Release)
        Assert.Equal(2, parsed.Changes.Length)
        Assert.Equal(BreakingChange, parsed.Changes[0].Category)
        Assert.Equal(DependencyChange, parsed.Changes[1].Category)

[<Fact>]
let ``parser preserves an unrecognized category as an unknown change`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            {
              "id": "abc123",
              "summary": "Tune background processing",
              "category": "performance"
            }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Error errors -> failwith $"Expected valid input, received: {errors}"
    | Ok parsed -> Assert.Equal(Unknown(Some "performance"), parsed.Changes[0].Category)

[<Fact>]
let ``parser reports all invalid change fields with actionable paths`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            { "id": "", "summary": "Valid" },
            { "id": "def456" }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Ok _ -> failwith "Expected input validation to fail."
    | Error errors ->
        Assert.Equal(2, errors.Length)
        Assert.Contains("changes[0].id", errors[0])
        Assert.Contains("changes[1].summary", errors[1])

[<Fact>]
let ``parser rejects duplicate change identifiers`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            { "id": "abc123", "summary": "First fix", "category": "fix" },
            { "id": "abc123", "summary": "Second fix", "category": "fix" }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Ok _ -> failwith "Expected duplicate identifiers to fail validation."
    | Error errors -> Assert.Contains("appears 2 times", errors[0])

[<Fact>]
let ``assessment uses explainable category weights and caps total score`` () =
    let releaseInput =
        { Release = "3.0.0"
          Changes =
            [ { Id = "breaking"
                Summary = "Break API"
                Category = BreakingChange
                Details = None }
              { Id = "security"
                Summary = "Patch authorization"
                Category = SecurityFix
                Details = None }
              { Id = "migration"
                Summary = "Migrate state"
                Category = Migration
                Details = None }
              { Id = "dependency"
                Summary = "Update runtime"
                Category = DependencyChange
                Details = None }
              { Id = "unknown"
                Summary = "Unclassified work"
                Category = Unknown None
                Details = None } ] }

    let report = RiskAssessment.assess releaseInput

    Assert.Equal(100, report.Score)
    Assert.Equal(Critical, report.Level)
    Assert.Equal<int list>([ 40; 25; 20; 15; 10 ], report.Changes |> List.map _.Score)

[<Theory>]
[<InlineData(0, "Low")>]
[<InlineData(2, "Low")>]
[<InlineData(10, "Moderate")>]
[<InlineData(30, "High")>]
[<InlineData(60, "Critical")>]
let ``assessment derives stable risk thresholds`` expectedScore expectedLevel =
    let scoreCategory =
        match expectedScore with
        | 0 -> []
        | 2 -> [ Fix ]
        | 10 -> [ Unknown None ]
        | 30 -> [ Migration; Unknown None ]
        | 60 -> [ BreakingChange; Migration ]
        | value -> failwith $"Unexpected test score {value}."

    let releaseInput =
        { Release = "threshold"
          Changes =
            scoreCategory
            |> List.mapi (fun index category ->
                { Id = string index
                  Summary = "Threshold input"
                  Category = category
                  Details = None }) }

    let report = RiskAssessment.assess releaseInput

    Assert.Equal(expectedScore, report.Score)
    Assert.Equal(expectedLevel, RiskLevel.displayName report.Level)

[<Fact>]
let ``markdown output explains score contributors in input order`` () =
    let output =
        { Release = "1.2.0"
          Changes =
            [ { Id = "one"
                Summary = "Update a dependency"
                Category = DependencyChange
                Details = None }
              { Id = "two"
                Summary = "Fix a crash"
                Category = Fix
                Details = None } ] }
        |> RiskAssessment.assess
        |> ReportOutput.renderMarkdown

    Assert.Contains("- Risk: **Moderate**", output)
    Assert.Contains("- Score: **17 / 100**", output)
    Assert.True(output.IndexOf("| one |") < output.IndexOf("| two |"))
    Assert.Contains("Dependency change can alter transitive behavior", output)

[<Fact>]
let ``json output is deterministic and uses a stable property order`` () =
    let report =
        { Release = "1.2.0"
          Changes =
            [ { Id = "one"
                Summary = "Tune the release workflow"
                Category = Unknown(Some "operations")
                Details = Some "Review manually." } ] }
        |> RiskAssessment.assess

    let first = ReportOutput.renderJson report
    let second = ReportOutput.renderJson report

    Assert.Equal(first, second)
    Assert.True(first.IndexOf("\"release\"") < first.IndexOf("\"score\""))
    Assert.True(first.IndexOf("\"score\"") < first.IndexOf("\"risk\""))

    use document = JsonDocument.Parse(first)
    let change = document.RootElement.GetProperty("changes")[0]
    Assert.Equal("unknown", change.GetProperty("category").GetString())
    Assert.Equal("operations", change.GetProperty("sourceCategory").GetString())

[<Fact>]
let ``cli defaults to markdown and standard input`` () =
    Assert.Equal(
        Ok(
            Run
                { InputPath = None
                  Format = Markdown
                  OutputPath = None }
        ),
        Cli.parse [||]
    )

[<Fact>]
let ``cli rejects unsupported output formats with a corrective message`` () =
    match Cli.parse [| "--format"; "yaml" |] with
    | Ok _ -> failwith "Expected an unsupported format error."
    | Error error -> Assert.Contains("Use 'markdown' or 'json'", error)
