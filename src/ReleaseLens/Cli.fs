namespace ReleaseLens

open System

type OutputFormat =
    | Markdown
    | Json

type CliOptions =
    { InputPath: string option
      Format: OutputFormat
      OutputPath: string option }

type CliCommand =
    | Run of CliOptions
    | ShowHelp

module Cli =
    let usage =
        """ReleaseLens - explainable release-risk reports

Usage:
  releaselens [INPUT] [--format markdown|json] [--output PATH]
  releaselens --help

Arguments:
  INPUT                JSON input file. Omit it or use '-' to read standard input.

Options:
  --format FORMAT      Output format: markdown (default) or json.
  --output PATH        Create a new report file instead of using standard output.
                       Existing files are never overwritten.
  -h, --help           Show this help text.
"""

    let private parseFormat =
        function
        | "markdown" -> Ok Markdown
        | "json" -> Ok Json
        | value -> Error $"Output format '{value}' is not supported. Use 'markdown' or 'json'."

    let parse args =
        let rec loop remaining inputPath outputFormat outputPath =
            match remaining with
            | [] ->
                Ok(
                    Run
                        { InputPath = inputPath
                          Format = outputFormat |> Option.defaultValue Markdown
                          OutputPath = outputPath }
                )
            | ("-h" | "--help") :: _ -> Ok ShowHelp
            | "--format" :: [] -> Error "Option '--format' requires 'markdown' or 'json'."
            | "--format" :: _ when Option.isSome outputFormat ->
                Error "Option '--format' was provided more than once. Provide exactly one output format."
            | "--format" :: value :: tail ->
                match parseFormat value with
                | Ok parsedFormat -> loop tail inputPath (Some parsedFormat) outputPath
                | Error error -> Error error
            | "--output" :: [] -> Error "Option '--output' requires a destination path."
            | "--output" :: _ when Option.isSome outputPath ->
                Error "Option '--output' was provided more than once. Provide exactly one destination path."
            | "--output" :: value :: _ when String.IsNullOrWhiteSpace(value) ->
                Error "Output path must not be empty or whitespace."
            | "--output" :: value :: _ when value.StartsWith("-") ->
                Error $"Option '--output' requires a destination path, but received option-like value '{value}'."
            | "--output" :: value :: tail -> loop tail inputPath outputFormat (Some value)
            | option :: _ when option.StartsWith("-") && option <> "-" ->
                Error $"Option '{option}' is not recognized. Run 'releaselens --help' for supported options."
            | input :: _ when String.IsNullOrWhiteSpace(input) -> Error "Input path must not be empty or whitespace."
            | input :: tail ->
                match inputPath with
                | None -> loop tail (Some input) outputFormat outputPath
                | Some existing ->
                    Error
                        $"Multiple input paths were provided: '{existing}' and '{input}'. Provide one input file or read standard input."

        loop (List.ofArray args) None None None
