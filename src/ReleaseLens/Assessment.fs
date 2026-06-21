namespace ReleaseLens

module RiskAssessment =
    let private scoreAndExplanation =
        function
        | BreakingChange -> 40, "Breaking change requires consumer compatibility review."
        | DependencyChange -> 15, "Dependency change can alter transitive behavior or deployment compatibility."
        | Migration -> 20, "Migration requires coordinated rollout or state transformation."
        | SecurityFix -> 25, "Security fix makes rollout timing and regression review especially important."
        | Fix -> 2, "Ordinary fix carries a small baseline regression risk."
        | Unknown None -> 10, "Unclassified change needs manual review before release."
        | Unknown(Some category) -> 10, $"Unrecognized category '{category}' needs manual review before release."

    let private levelForScore =
        function
        | score when score >= 60 -> Critical
        | score when score >= 30 -> High
        | score when score >= 10 -> Moderate
        | _ -> Low

    let assess (releaseInput: ReleaseInput) =
        let assessedChanges =
            releaseInput.Changes
            |> List.map (fun (change: ReleaseChange) ->
                let score, explanation = scoreAndExplanation change.Category

                { Change = change
                  Score = score
                  Explanation = explanation })

        let score = assessedChanges |> List.sumBy _.Score |> min 100

        { Release = releaseInput.Release
          Score = score
          Level = levelForScore score
          Changes = assessedChanges }
