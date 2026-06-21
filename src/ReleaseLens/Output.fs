namespace ReleaseLens

open System.IO
open System.Text
open System.Text.Json

module ReportOutput =
    let private categorySummaryOrder =
        [ "breaking", "Breaking changes"
          "dependency", "Dependency changes"
          "migration", "Migrations"
          "security", "Security fixes"
          "fix", "Ordinary fixes"
          "unknown", "Unknown or unclassified changes" ]

    let private categoryCount category (report: RiskReport) =
        report.Changes
        |> List.filter (fun assessed -> ChangeCategory.canonicalName assessed.Change.Category = category)
        |> List.length

    let private markdownText (value: string) =
        let flattened =
            value.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ")

        [ "\\", "\\\\"
          "`", "\\`"
          "*", "\\*"
          "_", "\\_"
          "[", "\\["
          "]", "\\]"
          "<", "\\<"
          ">", "\\>"
          "|", "\\|" ]
        |> List.fold
            (fun (text: string) (character: string, escaped: string) -> text.Replace(character, escaped))
            flattened

    let renderMarkdown (report: RiskReport) =
        let summaryLines =
            categorySummaryOrder
            |> List.choose (fun (category, label) ->
                let count = categoryCount category report

                if count = 0 then None else Some $"- {label}: {count}")

        let changeLines =
            report.Changes
            |> List.map (fun assessed ->
                let change = assessed.Change

                $"| {markdownText change.Id} | {markdownText change.Summary} | {markdownText (ChangeCategory.displayName change.Category)} | {assessed.Score} | {markdownText assessed.Explanation} |")

        [ $"# Release risk report: {markdownText report.Release}"
          ""
          $"- Risk: **{RiskLevel.displayName report.Level}**"
          $"- Score: **{report.Score} / 100**"
          ""
          "## Why"
          ""
          yield!
              if List.isEmpty summaryLines then
                  [ "- No changes were supplied." ]
              else
                  summaryLines
          ""
          "## Changes"
          ""
          yield!
              if List.isEmpty changeLines then
                  [ "No changes were supplied." ]
              else
                  [ "| ID | Summary | Category | Score | Explanation |"
                    "| --- | --- | --- | ---: | --- |"
                    yield! changeLines ] ]
        |> String.concat "\n"
        |> fun output -> output + "\n"

    let private writeSummary (writer: Utf8JsonWriter) (report: RiskReport) =
        writer.WriteStartArray("summary")

        for category, label in categorySummaryOrder do
            let count = categoryCount category report

            writer.WriteStartObject()
            writer.WriteString("category", category)
            writer.WriteString("label", label)
            writer.WriteNumber("count", count)
            writer.WriteEndObject()

        writer.WriteEndArray()

    let private writeChange (writer: Utf8JsonWriter) (assessed: AssessedChange) =
        writer.WriteStartObject()
        writer.WriteString("id", assessed.Change.Id)
        writer.WriteString("summary", assessed.Change.Summary)
        writer.WriteString("category", ChangeCategory.canonicalName assessed.Change.Category)

        match assessed.Change.Category with
        | Unknown(Some originalCategory) -> writer.WriteString("sourceCategory", originalCategory)
        | _ -> ()

        writer.WriteNumber("score", assessed.Score)
        writer.WriteString("explanation", assessed.Explanation)

        match assessed.Change.Details with
        | Some details -> writer.WriteString("details", details)
        | None -> ()

        writer.WriteEndObject()

    let renderJson (report: RiskReport) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteString("release", report.Release)
        writer.WriteNumber("score", report.Score)
        writer.WriteString("risk", RiskLevel.displayName report.Level)
        writeSummary writer report
        writer.WriteStartArray("changes")

        for assessed in report.Changes do
            writeChange writer assessed

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        Encoding.UTF8.GetString(stream.ToArray()) + "\n"
