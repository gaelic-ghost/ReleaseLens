namespace ReleaseLens

open System
open System.IO
open System.Text

module Program =
    [<Literal>]
    let private maxInputBytes = 1_048_576L

    [<Literal>]
    let private maxInputCharacters = 1_048_576

    [<Literal>]
    let private maxInputSizeDisplay = "1,048,576"

    let private readStandardInputBounded (standardInput: TextReader) =
        let buffer = Array.zeroCreate<char> 4096
        let output = StringBuilder()
        let mutable totalCharacters = 0
        let mutable keepReading = true
        let mutable tooLarge = false

        while keepReading && not tooLarge do
            let readCount = standardInput.Read(buffer, 0, buffer.Length)

            if readCount = 0 then
                keepReading <- false
            else
                totalCharacters <- totalCharacters + readCount

                if totalCharacters > maxInputCharacters then
                    tooLarge <- true
                else
                    output.Append(buffer, 0, readCount) |> ignore

        if tooLarge then
            Error
                $"Reading release input from 'standard input' stopped because it is larger than {maxInputSizeDisplay} characters. Split the release input or keep only the changes for one release."
        else
            Ok(output.ToString())

    let private readFileBounded path =
        let fileInfo = FileInfo(path)

        if fileInfo.Exists && fileInfo.Length > maxInputBytes then
            Error
                $"Reading release input from '{path}' stopped because the file is larger than {maxInputSizeDisplay} bytes. Split the release input or keep only the changes for one release."
        else
            Ok(File.ReadAllText(path))

    let private readInput (standardInput: TextReader) (inputPath: string option) =
        try
            match inputPath with
            | None
            | Some "-" -> readStandardInputBounded standardInput
            | Some path -> readFileBounded path
        with exceptionDetails ->
            let source = inputPath |> Option.defaultValue "standard input"

            Error
                $"Reading release input from '{source}' failed because: {exceptionDetails.Message} Verify that the path exists and is readable."

    let private temporarySiblingPath outputPath =
        let fullPath = Path.GetFullPath(outputPath)
        let directory = Path.GetDirectoryName(fullPath)
        let fileName = Path.GetFileName(fullPath)

        if String.IsNullOrWhiteSpace(directory) then
            Path.Combine(Directory.GetCurrentDirectory(), $".{fileName}.{Guid.NewGuid():N}.tmp")
        else
            Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp")

    let private tryDeleteTemporaryFile path =
        try
            if File.Exists(path) then
                File.Delete(path)
        with _ ->
            ()

    let private writeFileAtomic path (output: string) =
        if File.Exists(path) || Directory.Exists(path) then
            Error
                $"Writing the release report to '{path}' was stopped because the destination already exists. Choose a different path or remove the existing file first."
        else
            let temporaryPath = temporarySiblingPath path

            try
                do
                    use stream =
                        new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)

                    use writer = new StreamWriter(stream, UTF8Encoding(false))
                    writer.Write(output)

                File.Move(temporaryPath, path, false)
                Ok()
            with exceptionDetails ->
                tryDeleteTemporaryFile temporaryPath

                if File.Exists(path) || Directory.Exists(path) then
                    Error
                        $"Writing the release report to '{path}' was stopped because the destination already exists. Choose a different path or remove the existing file first."
                else
                    Error
                        $"Writing the release report to '{path}' failed because: {exceptionDetails.Message} Verify that the destination directory exists and is writable."

    let private writeOutput (standardOutput: TextWriter) (outputPath: string option) (output: string) =
        try
            match outputPath with
            | None ->
                standardOutput.Write(output)
                Ok()
            | Some path -> writeFileAtomic path output
        with exceptionDetails ->
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
