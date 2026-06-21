module ReleaseLens.Tests.ParserTests

open ReleaseLens
open Xunit

let private expectParseError (expectedText: string) (input: string) =
    match ReleaseInputParser.parse input with
    | Ok parsed -> failwith $"Expected input validation to fail, but parsed release '{parsed.Release}'."
    | Error errors -> Assert.Contains(errors, fun error -> error.Contains(expectedText))

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
let ``parser matches known categories case insensitively after trimming`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            {
              "id": "abc123",
              "summary": "Patch authorization",
              "category": "  SeCuRiTy  "
            }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Error errors -> failwith $"Expected valid input, received: {errors}"
    | Ok parsed -> Assert.Equal(SecurityFix, parsed.Changes[0].Category)

[<Fact>]
let ``parser preserves trimmed original text for an unrecognized category`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            {
              "id": "abc123",
              "summary": "Tune background processing",
              "category": "  Performance-Review  "
            }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Error errors -> failwith $"Expected valid input, received: {errors}"
    | Ok parsed -> Assert.Equal(Unknown(Some "Performance-Review"), parsed.Changes[0].Category)

[<Fact>]
let ``parser reports invalid change fields with actionable paths`` () =
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
let ``parser rejects malformed JSON shapes and wrong typed fields`` () =
    [ "Input root", "null"
      "Input root", "[]"
      "'release' must be a string", """{"release":null,"changes":[]}"""
      "'changes' must be a JSON array", """{"release":"1.0.0","changes":null}"""
      "'changes[0]' must be a JSON object", """{"release":"1.0.0","changes":[null]}"""
      "'changes[0].id' must be a string", """{"release":"1.0.0","changes":[{"id":1,"summary":"Fix"}]}"""
      "'changes[0].category' must be a string or null",
      """{"release":"1.0.0","changes":[{"id":"one","summary":"Fix","category":1}]}"""
      "'changes[0].details' must be a string or null",
      """{"release":"1.0.0","changes":[{"id":"one","summary":"Fix","details":{}}]}""" ]
    |> List.iter (fun (expectedText, input) -> expectParseError expectedText input)

[<Fact>]
let ``parser rejects duplicate JSON property names`` () =
    expectParseError
        "property 'release' appears more than once"
        """{"release":"1.0.0","release":"2.0.0","changes":[]}"""

    expectParseError
        "property 'changes[0].id' appears more than once"
        """{"release":"1.0.0","changes":[{"id":"one","id":"two","summary":"Fix"}]}"""

[<Fact>]
let ``parser rejects duplicate identifiers after trimming whitespace`` () =
    let input =
        """
        {
          "release": "1.1.0",
          "changes": [
            { "id": "abc123", "summary": "First fix", "category": "fix" },
            { "id": "  abc123  ", "summary": "Second fix", "category": "fix" }
          ]
        }
        """

    match ReleaseInputParser.parse input with
    | Ok _ -> failwith "Expected duplicate identifiers to fail validation."
    | Error errors -> Assert.Contains("appears 2 times", errors[0])

[<Fact>]
let ``parser accepts an empty change list`` () =
    match ReleaseInputParser.parse """{"release":"1.0.0","changes":[]}""" with
    | Error errors -> failwith $"Expected an empty release to be valid, received: {errors}"
    | Ok parsed -> Assert.Empty(parsed.Changes)
