namespace ReleaseLens

open System
open System.IO
open System.Text

module Program =
    let private readInput (standardInput: TextReader) (inputPath: string option) =
        try
            match inputPath with
            | None
            | Some "-" -> Ok(standardInput.ReadToEnd())
            | Some path -> Ok(File.ReadAllText(path))
        with exceptionDetails ->
            let source = inputPath |> Option.defaultValue "standard input"

            Error
                $"Reading release input from '{source}' failed because: {exceptionDetails.Message} Verify that the path exists and is readable."

    let private writeOutput (standardOutput: TextWriter) (outputPath: string option) (output: string) =
        try
            match outputPath with
            | None -> standardOutput.Write(output)
            | Some path ->
                use stream =
                    new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)

                use writer = new StreamWriter(stream, UTF8Encoding(false))
                writer.Write(output)

            Ok()
        with
        | :? IOException when outputPath |> Option.exists File.Exists ->
            let destination = outputPath |> Option.defaultValue "standard output"

            Error
                $"Writing the release report to '{destination}' was stopped because the destination already exists. Choose a different path or remove the existing file first."
        | exceptionDetails ->
            let destination = outputPath |> Option.defaultValue "standard output"

            Error
                $"Writing the release report to '{destination}' failed because: {exceptionDetails.Message} Verify that the destination directory exists and is writable."

    let private reportError (standardError: TextWriter) message =
        standardError.WriteLine($"ReleaseLens: {message}")

    let run (standardInput: TextReader) (standardOutput: TextWriter) (standardError: TextWriter) args =
        match Cli.parse args with
        | Error error ->
            reportError standardError error
            2
        | Ok ShowHelp ->
            standardOutput.Write(Cli.usage)
            0
        | Ok(Run options) ->
            match readInput standardInput options.InputPath with
            | Error error ->
                reportError standardError error
                1
            | Ok input ->
                match ReleaseInputParser.parse input with
                | Error errors ->
                    errors |> List.iter (reportError standardError)
                    1
                | Ok releaseInput ->
                    let report = RiskAssessment.assess releaseInput

                    let output =
                        match options.Format with
                        | Markdown -> ReportOutput.renderMarkdown report
                        | Json -> ReportOutput.renderJson report

                    match writeOutput standardOutput options.OutputPath output with
                    | Ok() -> 0
                    | Error error ->
                        reportError standardError error
                        1

    [<EntryPoint>]
    let main args =
        run Console.In Console.Out Console.Error args
