namespace ReleaseLens

type ChangeCategory =
    | BreakingChange
    | DependencyChange
    | Migration
    | SecurityFix
    | Fix
    | Unknown of originalCategory: string option

module ChangeCategory =
    let canonicalName =
        function
        | BreakingChange -> "breaking"
        | DependencyChange -> "dependency"
        | Migration -> "migration"
        | SecurityFix -> "security"
        | Fix -> "fix"
        | Unknown _ -> "unknown"

    let displayName =
        function
        | BreakingChange -> "Breaking change"
        | DependencyChange -> "Dependency change"
        | Migration -> "Migration"
        | SecurityFix -> "Security fix"
        | Fix -> "Fix"
        | Unknown None -> "Unknown"
        | Unknown(Some originalCategory) -> $"Unknown ({originalCategory})"

    let fromInput (value: string option) =
        match value |> Option.map (fun category -> category.Trim().ToLowerInvariant()) with
        | None
        | Some "" -> Unknown None
        | Some "breaking"
        | Some "breaking-change"
        | Some "breaking_change" -> BreakingChange
        | Some "dependency"
        | Some "dependency-change"
        | Some "dependency_change" -> DependencyChange
        | Some "migration" -> Migration
        | Some "security"
        | Some "security-fix"
        | Some "security_fix" -> SecurityFix
        | Some "fix" -> Fix
        | Some originalCategory -> Unknown(Some originalCategory)

type ReleaseChange =
    { Id: string
      Summary: string
      Category: ChangeCategory
      Details: string option }

type ReleaseInput =
    { Release: string
      Changes: ReleaseChange list }

type RiskLevel =
    | Low
    | Moderate
    | High
    | Critical

module RiskLevel =
    let displayName =
        function
        | Low -> "Low"
        | Moderate -> "Moderate"
        | High -> "High"
        | Critical -> "Critical"

type AssessedChange =
    { Change: ReleaseChange
      Score: int
      Explanation: string }

type RiskReport =
    { Release: string
      Score: int
      Level: RiskLevel
      Changes: AssessedChange list }
