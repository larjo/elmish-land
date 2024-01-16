module ElmishLand.TemplateEngine

open System
open System.Text.RegularExpressions
open HandlebarsDotNet
open ElmishLand.Base
open Microsoft.FSharp.Collections
open Orsak
open ElmishLand.Resource

let handlebars model (src: string) =
    let handlebars = Handlebars.Create()
    handlebars.Configuration.ThrowOnUnresolvedBindingExpression <- true
    handlebars.Configuration.NoEscape <- true

    try
        handlebars.Compile(src).Invoke(model)
    with ex ->
        raise (Exception($"Handlebars compilation failed.\n%s{src}\n%A{model}", ex))

type Route = {
    Name: string
    RouteName: string
    MsgName: string
    ModuleName: string
    RecordDefinition: string
    RecordConstructor: string
    RecordConstructorWithQuery: string
    RecordPattern: string
    UrlUsage: string
    UrlPattern: string
    UrlPatternWithQuery: string
    UrlPatternWhen: string
}

type RouteData = {
    Autogenerated: string
    RootModule: string
    Routes: Route array
}

let getSortedPageFiles (projectDir: AbsoluteProjectDir) =
    eff {
        let! fs = getFileSystem ()

        return
            fs.GetFilesRecursive(
                FilePath.appendParts [ "src"; "Pages" ] (AbsoluteProjectDir.asFilePath projectDir),
                "Page.fs"
            )
            |> Array.map FilePath.fromString
            |> Array.sortByDescending (fun x ->
                if x |> FilePath.endsWithParts [ "src"; "Pages"; "Home"; "Page.fs" ] then
                    0
                else
                    x |> FilePath.parts |> Array.length)
    }

let wrapWithTicksIfNeeded (s: string) =
    if Regex.IsMatch(s, "^[0-9a-zA-Z_]+$") && s <> "id" then
        s
    else
        $"``%s{s}``"

let toPascalCase (s: string) = $"%s{s[0..0].ToUpper()}{s[1..]}"
let toCamelCase (s: string) = $"%s{s[0..0].ToLower()}%s{s[1..]}"

let fileToRoute (projectDir: AbsoluteProjectDir) (FilePath file) =
    let route =
        file[0 .. file.Length - 9]
        |> String.replace
            (AbsoluteProjectDir.asFilePath projectDir
             |> FilePath.appendParts [ "src"; "Pages" ]
             |> FilePath.asString)
            ""
        |> String.replace "\\" "/"

    route[1..]
    |> String.split "/"
    |> Array.fold
        (fun (parts, args) part ->
            if part.StartsWith("_") then
                (toPascalCase part).TrimStart('_') :: parts, (toCamelCase part).TrimStart('_') :: args
            else
                toPascalCase part :: parts, args)
        ([], [])
    |> fun (parts, args) ->
        let args = List.rev args

        let name =
            parts
            |> List.rev
            |> List.map toPascalCase
            |> String.concat ""
            |> fun name -> if name = "" then "Home" else name

        let recordPattern =
            let argString =
                args
                |> List.map (fun arg ->
                    $"%s{arg |> toPascalCase |> wrapWithTicksIfNeeded} = %s{wrapWithTicksIfNeeded arg}")
                |> String.concat "; "

            let argString = if argString.Length = 0 then "" else $"%s{argString}; "
            $"{{ %s{argString}Query = query }}"

        let recordDefinition =
            args
            |> List.map (fun arg -> $"%s{arg |> toPascalCase |> wrapWithTicksIfNeeded}: string")
            |> String.concat "; "
            |> fun x ->
                let x = if String.IsNullOrWhiteSpace x then "" else $"%s{x}; "
                $"{{ %s{x}Query: list<string * string> }}"

        let recordConstructor hasNonEmptyQuery =
            let argString =
                args
                |> List.map (fun arg ->
                    $"%s{arg |> toPascalCase |> wrapWithTicksIfNeeded} = %s{wrapWithTicksIfNeeded arg}")
                |> String.concat "; "

            let argString = if argString.Length = 0 then "" else $"%s{argString}; "
            let query = if hasNonEmptyQuery then "query" else "[]"
            $"{{ %s{argString}Query = %s{query} }}"

        let urlUsage =
            if route = "/Home" then "/" else route
            |> String.split "/"
            |> Array.map (fun x -> if x.StartsWith("_") then x.TrimStart('_') else $"\"%s{x}\"")
            |> String.concat ", "

        let lowerCaseArgs = args |> List.map (fun x -> x.ToLowerInvariant())

        let urlPattern includeQuery =
            (if route = "/Home" then "/" else route)
            |> String.split "/"
            |> Array.map (fun x -> if x.StartsWith "_" then x.TrimStart('_') else x)
            |> Array.map (fun arg -> arg.ToLowerInvariant())
            |> Array.map wrapWithTicksIfNeeded
            |> String.concat "; "
            |> fun pattern ->
                if pattern.Length > 0 then
                    let query = if includeQuery then "; Query query" else ""
                    $"[ %s{pattern}{query} ]"
                else
                    let query = if includeQuery then "Query query" else ""
                    $"[ %s{query} ]"

        let urlPatternWhen =
            (if route = "/Home" then "/" else route)
            |> String.split "/"
            |> Array.choose (fun x -> if x.StartsWith "_" then None else Some x)
            |> Array.map (fun arg -> arg.ToLowerInvariant())
            |> Array.map (fun arg -> $"eq %s{wrapWithTicksIfNeeded arg} \"%s{arg}\"")
            |> String.concat " && "

        {
            Name = wrapWithTicksIfNeeded name
            RouteName = wrapWithTicksIfNeeded $"%s{name}Route"
            MsgName = wrapWithTicksIfNeeded $"%s{name}Msg"
            ModuleName =
                $"%s{projectDir
                     |> AbsoluteProjectDir.asFilePath
                     |> FileName.fromFilePath
                     |> FileName.asString}.Pages.%s{wrapWithTicksIfNeeded name}.Page"
            RecordDefinition = recordDefinition
            RecordConstructor = recordConstructor false
            RecordConstructorWithQuery = recordConstructor true
            RecordPattern = recordPattern
            UrlUsage = urlUsage
            UrlPattern = urlPattern false
            UrlPatternWithQuery = urlPattern true
            UrlPatternWhen = urlPatternWhen
        }

let getRouteData (projectDir: AbsoluteProjectDir) =
    eff {
        let! pageFiles = getSortedPageFiles projectDir

        return {
            Autogenerated = getAutogenerated ()
            RootModule =
                projectDir
                |> AbsoluteProjectDir.asFilePath
                |> FileName.fromFilePath
                |> FileName.asString
                |> wrapWithTicksIfNeeded
            Routes = pageFiles |> Array.map (fileToRoute projectDir)
        }
    }

let generateFiles projectDir (routeData: RouteData) =
    let writeResource = writeResource projectDir true

    eff {
        do! writeResource "Routes.handlebars" [ ".elmish-land"; "Base"; "Routes.fs" ] (Some(handlebars routeData))
        do! writeResource "Command.fs.handlebars" [ ".elmish-land"; "Base"; "Command.fs" ] (Some(handlebars routeData))
        do! writeResource "Page.fs.handlebars" [ ".elmish-land"; "Base"; "Page.fs" ] (Some(handlebars routeData))
        do! writeResource "Layout.fs.handlebars" [ ".elmish-land"; "Base"; "Layout.fs" ] (Some(handlebars routeData))
        do! writeResource "App.handlebars" [ ".elmish-land"; "App"; "App.fs" ] (Some(handlebars routeData))
    }
