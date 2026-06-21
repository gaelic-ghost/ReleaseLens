namespace ReleaseLens

open System
open System.Text.Json

module ReleaseInputParser =
    let private tryGetProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> Some value
        | false, _ -> None

    let private readRequiredString path (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.String then
            Error $"Input property '{path}' must be a string."
        else
            let value = element.GetString()

            if String.IsNullOrWhiteSpace(value) then
                Error $"Input property '{path}' must be a non-empty string."
            else
                Ok(value.Trim())

    let private readOptionalString path (element: JsonElement option) =
        match element with
        | None -> Ok None
        | Some element when element.ValueKind = JsonValueKind.Null -> Ok None
        | Some element when element.ValueKind = JsonValueKind.String ->
            let value = element.GetString()

            if String.IsNullOrWhiteSpace(value) then
                Ok None
            else
                Ok(Some(value.Trim()))
        | Some _ -> Error $"Input property '{path}' must be a string or null when provided."

    let private parseChange index (element: JsonElement) =
        let path propertyName = $"changes[{index}].{propertyName}"
        let idPath = path "id"
        let summaryPath = path "summary"

        if element.ValueKind <> JsonValueKind.Object then
            Error $"Input item 'changes[{index}]' must be a JSON object."
        else
            match tryGetProperty "id" element with
            | None -> Error $"Input property '{idPath}' is required."
            | Some idElement ->
                match readRequiredString idPath idElement with
                | Error error -> Error error
                | Ok id ->
                    match tryGetProperty "summary" element with
                    | None -> Error $"Input property '{summaryPath}' is required."
                    | Some summaryElement ->
                        match readRequiredString summaryPath summaryElement with
                        | Error error -> Error error
                        | Ok summary ->
                            match readOptionalString (path "category") (tryGetProperty "category" element) with
                            | Error error -> Error error
                            | Ok category ->
                                match readOptionalString (path "details") (tryGetProperty "details" element) with
                                | Error error -> Error error
                                | Ok details ->
                                    Ok
                                        { Id = id
                                          Summary = summary
                                          Category = ChangeCategory.fromInput category
                                          Details = details }

    let private collectResults results =
        let values, errors =
            results
            |> Array.fold
                (fun (values, errors) result ->
                    match result with
                    | Ok value -> value :: values, errors
                    | Error error -> values, error :: errors)
                ([], [])

        match errors with
        | [] -> Ok(List.rev values)
        | _ -> Error(List.rev errors)

    let private duplicateIdErrors (changes: ReleaseChange list) =
        changes
        |> List.countBy _.Id
        |> List.choose (fun (id, count) ->
            if count > 1 then
                Some $"Input change id '{id}' appears {count} times. Every change id must be unique."
            else
                None)

    let parse (json: string) =
        try
            use document = JsonDocument.Parse(json)
            let root = document.RootElement

            if root.ValueKind <> JsonValueKind.Object then
                Error [ "Input root must be a JSON object with 'release' and 'changes' properties." ]
            else
                match tryGetProperty "release" root with
                | None -> Error [ "Input property 'release' is required." ]
                | Some releaseElement ->
                    match readRequiredString "release" releaseElement with
                    | Error error -> Error [ error ]
                    | Ok release ->
                        match tryGetProperty "changes" root with
                        | None -> Error [ "Input property 'changes' is required." ]
                        | Some changesElement when changesElement.ValueKind <> JsonValueKind.Array ->
                            Error [ "Input property 'changes' must be a JSON array." ]
                        | Some changesElement ->
                            let parsedChanges =
                                changesElement.EnumerateArray()
                                |> Seq.mapi parseChange
                                |> Seq.toArray
                                |> collectResults

                            match parsedChanges with
                            | Error errors -> Error errors
                            | Ok changes ->
                                match duplicateIdErrors changes with
                                | [] -> Ok { Release = release; Changes = changes }
                                | errors -> Error errors
        with :? JsonException as exceptionDetails ->
            Error [ $"Input is not valid JSON: {exceptionDetails.Message}" ]
