namespace ReleaseLens

open System
open System.IO

module Program =
    let private readInput (inputPath: string option) =
        try
            match inputPath with
            | None
            | Some "-" -> Ok(Console.In.ReadToEnd())
            | Some path -> Ok(File.ReadAllText(path))
        with exceptionDetails ->
            let source = inputPath |> Option.defaultValue "standard input"

            Error
                $"Reading release input from '{source}' failed because: {exceptionDetails.Message} Verify that the path exists and is readable."

    let private writeOutput (outputPath: string option) (output: string) =
        try
            match outputPath with
            | None -> Console.Out.Write(output)
            | Some path -> File.WriteAllText(path, output)

            Ok()
        with exceptionDetails ->
            let destination = outputPath |> Option.defaultValue "standard output"

            Error
                $"Writing the release report to '{destination}' failed because: {exceptionDetails.Message} Verify that the destination directory exists and is writable."

    let private reportError message =
        Console.Error.WriteLine($"ReleaseLens: {message}")

    [<EntryPoint>]
    let main args =
        match Cli.parse args with
        | Error error ->
            reportError error
            2
        | Ok ShowHelp ->
            Console.Out.Write(Cli.usage)
            0
        | Ok(Run options) ->
            match readInput options.InputPath with
            | Error error ->
                reportError error
                1
            | Ok input ->
                match ReleaseInputParser.parse input with
                | Error errors ->
                    errors |> List.iter reportError
                    1
                | Ok releaseInput ->
                    let report = RiskAssessment.assess releaseInput

                    let output =
                        match options.Format with
                        | Markdown -> ReportOutput.renderMarkdown report
                        | Json -> ReportOutput.renderJson report

                    match writeOutput options.OutputPath output with
                    | Ok() -> 0
                    | Error error ->
                        reportError error
                        1
