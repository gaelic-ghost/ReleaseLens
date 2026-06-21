module ReleaseLens.Tests.AssessmentTests

open ReleaseLens
open Xunit

let private change index category =
    { Id = string index
      Summary = "Threshold input"
      Category = category
      Details = None }

let private assess categories =
    { Release = "threshold"
      Changes = categories |> List.mapi change }
    |> RiskAssessment.assess

[<Fact>]
let ``assessment uses explainable category weights and caps total score`` () =
    let report =
        [ BreakingChange; SecurityFix; Migration; DependencyChange; Unknown None ]
        |> assess

    Assert.Equal(100, report.Score)
    Assert.Equal(Critical, report.Level)
    Assert.Equal<int list>([ 40; 25; 20; 15; 10 ], report.Changes |> List.map _.Score)

[<Fact>]
let ``assessment keeps every risk boundary stable`` () =
    let cases =
        [ [], 0, Low
          List.replicate 4 Fix, 8, Low
          List.replicate 5 Fix, 10, Moderate
          DependencyChange :: List.replicate 7 Fix, 29, Moderate
          DependencyChange :: Migration :: List.replicate 2 Fix, 39, High
          BreakingChange :: List.replicate 9 Fix, 58, High
          List.replicate 3 Migration, 60, Critical ]

    cases
    |> List.iter (fun (categories, expectedScore, expectedLevel) ->
        let report = assess categories
        Assert.Equal(expectedScore, report.Score)
        Assert.Equal(expectedLevel, report.Level))
