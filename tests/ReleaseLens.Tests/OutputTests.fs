module ReleaseLens.Tests.OutputTests

open System
open System.Text.Json
open ReleaseLens
open Xunit

let private report release changes =
    { Release = release; Changes = changes } |> RiskAssessment.assess

let private change id summary category details =
    { Id = id
      Summary = summary
      Category = category
      Details = details }

[<Fact>]
let ``markdown output explains score contributors in input order`` () =
    let output =
        report
            "1.2.0"
            [ change "one" "Update a dependency" DependencyChange None
              change "two" "Fix a crash" Fix None ]
        |> ReportOutput.renderMarkdown

    Assert.Contains("- Risk: **Moderate**", output)
    Assert.Contains("- Score: **17 / 100**", output)
    Assert.True(output.IndexOf("| one |") < output.IndexOf("| two |"))
    Assert.Contains("Dependency change can alter transitive behavior", output)

[<Fact>]
let ``markdown output escapes structure-changing input and flattens line breaks`` () =
    let output =
        report
            "2.0\n<script>*preview*"
            [ change "one|two" "Line one\r\nLine two [link]" (Unknown(Some "Ops|Review")) None ]
        |> ReportOutput.renderMarkdown

    Assert.StartsWith("# Release risk report: 2.0 \\<script\\>\\*preview\\*\n", output)
    Assert.Contains("| one\\|two | Line one Line two \\[link\\] | Unknown (Ops\\|Review) |", output)
    Assert.DoesNotContain("<script>", output)

[<Fact>]
let ``markdown output describes an empty release`` () =
    let output = report "1.0.0" [] |> ReportOutput.renderMarkdown

    Assert.Contains("- Risk: **Low**", output)
    Assert.Contains("- Score: **0 / 100**", output)
    Assert.Contains("- No changes were supplied.", output)

[<Fact>]
let ``json output is deterministic escaped and uses stable property ordering`` () =
    let assessed =
        report
            "1.2.0 \"preview\""
            [ change "one" "Tune\nworkflow" (Unknown(Some "Operations")) (Some "Review \\ manually.") ]

    let first = ReportOutput.renderJson assessed
    let second = ReportOutput.renderJson assessed

    Assert.Equal(first, second)
    Assert.EndsWith("\n", first)
    Assert.DoesNotContain("\r", first)
    Assert.True(first.IndexOf("\"release\"") < first.IndexOf("\"score\""))
    Assert.True(first.IndexOf("\"score\"") < first.IndexOf("\"risk\""))

    use document = JsonDocument.Parse(first)
    let root = document.RootElement
    let change = root.GetProperty("changes")[0]

    Assert.Equal("1.2.0 \"preview\"", root.GetProperty("release").GetString())
    Assert.Equal("Tune\nworkflow", change.GetProperty("summary").GetString())
    Assert.Equal("unknown", change.GetProperty("category").GetString())
    Assert.Equal("Operations", change.GetProperty("sourceCategory").GetString())
    Assert.Equal("Review \\ manually.", change.GetProperty("details").GetString())

[<Fact>]
let ``json category summaries always use canonical order`` () =
    let output = report "1.0.0" [] |> ReportOutput.renderJson
    use document = JsonDocument.Parse(output)

    let categories =
        document.RootElement.GetProperty("summary").EnumerateArray()
        |> Seq.map (fun item -> item.GetProperty("category").GetString())
        |> Seq.toList

    Assert.Equal<string list>([ "breaking"; "dependency"; "migration"; "security"; "fix"; "unknown" ], categories)
