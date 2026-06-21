module ReleaseLens.Tests.CliTests

open ReleaseLens
open Xunit

[<Fact>]
let ``cli defaults to markdown and standard input`` () =
    Assert.Equal(
        Ok(
            Run
                { InputPath = None
                  Format = Markdown
                  OutputPath = None }
        ),
        Cli.parse [||]
    )

[<Fact>]
let ``cli rejects unsupported output formats with a corrective message`` () =
    match Cli.parse [| "--format"; "yaml" |] with
    | Ok _ -> failwith "Expected an unsupported format error."
    | Error error -> Assert.Contains("Use 'markdown' or 'json'", error)

[<Fact>]
let ``cli rejects repeated options instead of silently overriding them`` () =
    match Cli.parse [| "--format"; "json"; "--format"; "markdown" |] with
    | Ok _ -> failwith "Expected repeated format options to fail."
    | Error error -> Assert.Contains("'--format' was provided more than once", error)

    match Cli.parse [| "--output"; "one.json"; "--output"; "two.json" |] with
    | Ok _ -> failwith "Expected repeated output options to fail."
    | Error error -> Assert.Contains("'--output' was provided more than once", error)

[<Fact>]
let ``cli rejects blank input and output paths`` () =
    match Cli.parse [| "" |] with
    | Ok _ -> failwith "Expected a blank input path to fail."
    | Error error -> Assert.Contains("Input path must not be empty", error)

    match Cli.parse [| "--output"; " " |] with
    | Ok _ -> failwith "Expected a blank output path to fail."
    | Error error -> Assert.Contains("Output path must not be empty", error)
