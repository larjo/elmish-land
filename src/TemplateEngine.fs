module ElmishLand.TemplateEngine

open System
open System.IO
open System.Text.RegularExpressions
open HandlebarsDotNet
open ElmishLand.Base
open Microsoft.FSharp.Collections

module String =
    let replace (oldValue: string) (newValue: string) (s: string) = s.Replace(oldValue, newValue)

    let split (separator: string) (s: string) =
        s.Split(separator, StringSplitOptions.RemoveEmptyEntries)

let handlebars (src: string) model =
    let handlebars = Handlebars.Create()
    handlebars.Configuration.ThrowOnUnresolvedBindingExpression <- true
    handlebars.Configuration.NoEscape <- true

    try
        handlebars.Compile(src).Invoke(model)
    with ex ->
        raise (Exception($"Handlebars compilation failed.\n%s{src}\n%A{model}", ex))

type Route = {
    Name: string
    ModuleName: string
    ArgsDefinition: string
    ArgsUsage: string
    ArgsPattern: string
    Url: string
    UrlPattern: string
    UrlPatternWithQuery: string
}

type RouteData = {
    Autogenerated: string
    RootModule: string
    Routes: Route array
}

let getSortedPageFiles (projectDir: AbsoluteProjectDir) =
    Directory.GetFiles(
        AbsoluteProjectDir.asString projectDir,
        "Page.fs",
        EnumerationOptions(RecurseSubdirectories = true)
    )
    |> Array.map FilePath.fromString
    |> Array.sortBy (fun x ->
        if x |> FilePath.endsWithParts [ "src"; "Pages"; "Page.fs" ] then
            ""
        else
            FilePath.asString x)

let quoteIfNeeded (s: string) =
    if Regex.IsMatch(s, "^[0-9a-zA-Z_]+$") then
        s
    else
        $"``%s{s}``"

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
            if part.StartsWith("{") && part.EndsWith("}") then
                $"%s{part[0..0].ToUpper()}{part[1..]}" :: parts,
                $"%s{part[1..1].ToLower()}%s{part[2 .. part.Length - 2]}" :: args
            else
                $"%s{part[0..0].ToUpper()}{part[1..]}" :: parts, args)
        ([], [])
    |> fun (parts, args) ->
        let args = List.rev args

        let name =
            String.concat "_" (List.rev parts)
            |> fun name -> if name = "" then "Home" else name

        let argsPattern =
            let argString =
                args
                |> List.map (fun arg -> $"%s{quoteIfNeeded arg}: string")
                |> String.concat ", "

            if argString.Length = 0 then "" else $"%s{argString}"

        let argsDefinition =
            let argString =
                args
                |> List.map (fun arg -> $"%s{quoteIfNeeded arg}: string")
                |> String.concat " * "

            if argString.Length = 0 then "" else $"%s{argString}"

        let argsUsage =
            let argString =
                args |> List.map (fun arg -> $"%s{quoteIfNeeded arg}") |> String.concat ", "

            if argString.Length = 0 then "" else $"%s{argString}"

        let url =
            args
            |> List.fold
                (fun (url: string) arg ->
                    url.Replace(
                        $"{{{arg}}}",
                        $"%%s{{%s{quoteIfNeeded arg}}}",
                        StringComparison.InvariantCultureIgnoreCase
                    ))
                (if route = "/Home" then "/" else route)

        let lowerCaseArgs = args |> List.map (fun x -> x.ToLowerInvariant())

        let urlPattern includeQuery =
            (if route = "/Home" then "/" else route)
            |> String.split "/"
            |> Array.map (fun x ->
                if x.StartsWith "{" && x.EndsWith "}" then
                    x[1 .. x.Length - 2] // Remove leading { and trailing }
                else
                    x)
            |> Array.map (fun arg -> arg.ToLowerInvariant())
            |> Array.map (fun arg ->
                if List.contains arg lowerCaseArgs then
                    quoteIfNeeded arg
                else
                    $"\"%s{arg}\"")
            |> String.concat "; "
            |> fun pattern ->
                let query = if includeQuery then "; Route.Query _" else ""

                if pattern.Length > 0 then
                    $"[ %s{pattern}{query} ]"
                else
                    "[]"

        {
            Name = quoteIfNeeded name
            ModuleName =
                sprintf
                    "%s.Pages.%s.Page"
                    (projectDir
                     |> AbsoluteProjectDir.asFilePath
                     |> FileName.fromFilePath
                     |> FileName.asString)
                    (name |> String.split "_" |> Array.map quoteIfNeeded |> String.concat ".")
            ArgsDefinition = argsDefinition
            ArgsUsage = argsUsage
            ArgsPattern = argsPattern
            Url = url
            UrlPattern = urlPattern false
            UrlPatternWithQuery = urlPattern true
        }

let getRouteData (projectDir: AbsoluteProjectDir) =
    let pageFiles = getSortedPageFiles projectDir

    {
        Autogenerated = autogenerated
        RootModule =
            projectDir
            |> AbsoluteProjectDir.asFilePath
            |> FileName.fromFilePath
            |> FileName.asString
            |> quoteIfNeeded
        Routes = pageFiles |> Array.map (fileToRoute projectDir)
    }

let processTemplate routeData (src: string) = handlebars src routeData

let generateRoutesAndApp projectDir (routeData: RouteData) =
    let copyFile = writeResource projectDir
    copyFile "Routes.handlebars" [ "src"; "Routes.fs" ] (Some(processTemplate routeData))
    copyFile "App.handlebars" [ "src"; "App.fs" ] (Some(processTemplate routeData))
