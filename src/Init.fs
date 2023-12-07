module ElmishLand.Init

open System.IO
open System.Threading
open ElmishLand.Base
open ElmishLand.TemplateEngine

let init (projectDir: AbsoluteProjectDir) =
    try
        let log = Log()
        log.Info("Initializing project. {}", AbsoluteProjectDir.asString projectDir)
        let projectName = projectDir |> ProjectName.fromProjectDir

        let writeResource = writeResource projectDir

        writeResource "PROJECT_NAME.fsproj" [ $"%s{(ProjectName.asString) projectName}.fsproj" ] None

        writeResource "global.json" [ "global.json" ] None
        writeResource "index.html" [ "index.html" ] None
        writeResource ".gitignore" [ ".gitignore" ] None

        writeResource
            "package.json"
            [ "package.json" ]
            (Some(fun x -> x.Replace("{{PROJECT_NAME}}" |> String.asKebabCase, ProjectName.asString projectName)))

        writeResource "dotnet-tools.json" [ ".config"; "dotnet-tools.json" ] None
        writeResource "settings.json" [ ".vscode"; "settings.json" ] None

        let rootModuleName = projectName |> ProjectName.asString |> quoteIfNeeded

        let homeRoute = {
            Name = "Home"
            ModuleName = $"%s{rootModuleName}.Pages.Home.Page"
            ArgsDefinition = ""
            ArgsUsage = ""
            ArgsPattern = ""
            Url = "/"
            UrlPattern = "[]"
            UrlPatternWithQuery = "[]"
        }

        let routeData = {
            Autogenerated = autogenerated.Value
            RootModule = rootModuleName
            Routes = [| homeRoute |]
        }

        writeResource "Shared.handlebars" [ "src"; "Shared.fs" ] (Some(processTemplate routeData))

        writeResource
            "Page.handlebars"
            [ "src"; "Pages"; "Home"; "Page.fs" ]
            (Some(
                processTemplate {|
                    RootModule = rootModuleName
                    Route = homeRoute
                |}
            ))

        generateRoutesAndApp projectDir routeData

        runProcesses [
            projectDir,
            "dotnet",
            [|
                "tool"
                "install"
                "elmish-land"
                if isPreRelease.Value then "--prerelease" else ()
            |],
            CancellationToken.None,
            ignore
            projectDir, "npm", [| "install" |], CancellationToken.None, ignore
        ]
        |> handleAppResult (fun () ->
            printfn
                $"""
    %s{commandHeader $"created a new project in ./%s{ProjectName.asString projectName}"}
    Run the following command to start the development server:

    dotnet elmish-land server
    """ )

    with :? IOException as ex ->
        printfn $"%s{ex.Message}"
        -1
