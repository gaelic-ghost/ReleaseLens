module ReleaseLens.Tests.ProgramTests

open System
open System.IO
open ReleaseLens
open Xunit

let private validInput =
    """{"release":"1.0.0","changes":[{"id":"one","summary":"Fix","category":"fix"}]}"""

let private run input arguments =
    use standardInput = new StringReader(input)
    use standardOutput = new StringWriter()
    use standardError = new StringWriter()

    let exitCode = Program.run standardInput standardOutput standardError arguments

    exitCode, standardOutput.ToString(), standardError.ToString()

let private withTemporaryDirectory action =
    let path =
        Path.Combine(Path.GetTempPath(), "ReleaseLens.Tests", Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(path) |> ignore

    try
        action path
    finally
        Directory.Delete(path, true)

[<Fact>]
let ``program refuses to overwrite an existing output file`` () =
    withTemporaryDirectory (fun directory ->
        let inputPath = Path.Combine(directory, "input.json")
        let outputPath = Path.Combine(directory, "report.json")
        File.WriteAllText(inputPath, validInput)
        File.WriteAllText(outputPath, "keep me")

        let exitCode, standardOutput, standardError =
            run "" [| inputPath; "--format"; "json"; "--output"; outputPath |]

        Assert.Equal(1, exitCode)
        Assert.Empty(standardOutput)
        Assert.Contains("destination already exists", standardError)
        Assert.Equal("keep me", File.ReadAllText(outputPath)))

[<Fact>]
let ``program refuses to replace its input file with output`` () =
    withTemporaryDirectory (fun directory ->
        let inputPath = Path.Combine(directory, "input.json")
        File.WriteAllText(inputPath, validInput)

        let exitCode, _, standardError =
            run "" [| inputPath; "--format"; "json"; "--output"; inputPath |]

        Assert.Equal(1, exitCode)
        Assert.Contains("destination already exists", standardError)
        Assert.Equal(validInput, File.ReadAllText(inputPath)))

[<Fact>]
let ``program reads standard input and writes only the requested report to standard output`` () =
    let exitCode, standardOutput, standardError =
        run validInput [| "--format"; "json" |]

    Assert.Equal(0, exitCode)
    Assert.Empty(standardError)
    Assert.StartsWith("{\n  \"release\": \"1.0.0\"", standardOutput)
    Assert.EndsWith("\n", standardOutput)

[<Fact>]
let ``program returns distinct argument and input failure exit codes`` () =
    let argumentExitCode, argumentOutput, argumentError =
        run "" [| "--format"; "yaml" |]

    Assert.Equal(2, argumentExitCode)
    Assert.Empty(argumentOutput)
    Assert.StartsWith("ReleaseLens: Output format", argumentError)

    let inputExitCode, inputOutput, inputError = run "{" [||]

    Assert.Equal(1, inputExitCode)
    Assert.Empty(inputOutput)
    Assert.StartsWith("ReleaseLens: Input is not valid JSON", inputError)

[<Fact>]
let ``program creates a new UTF-8 output file with no byte order mark`` () =
    withTemporaryDirectory (fun directory ->
        let outputPath = Path.Combine(directory, "report.json")

        let exitCode, standardOutput, standardError =
            run validInput [| "--format"; "json"; "--output"; outputPath |]

        let bytes = File.ReadAllBytes(outputPath)

        Assert.Equal(0, exitCode)
        Assert.Empty(standardOutput)
        Assert.Empty(standardError)
        Assert.NotEmpty(bytes)
        Assert.NotEqual<byte array>([| 0xEFuy; 0xBBuy; 0xBFuy |], bytes |> Array.truncate 3)
        Assert.Equal(0x0Auy, bytes[bytes.Length - 1]))
