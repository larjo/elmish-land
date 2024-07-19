module ElmishLand.AddPage

open Orsak
open ElmishLand.Base
open ElmishLand.Log
open ElmishLand.TemplateEngine
open ElmishLand.Resource

let addPage absoluteProjectDir (url: string) =
    eff {
        let! log = Log().Get()

        log.Debug("Working driectory: {}", FilePath.asString workingDirectory)
        log.Debug("Project directory: {}", absoluteProjectDir |> AbsoluteProjectDir.asFilePath |> FilePath.asString)

        let! projectPath = absoluteProjectDir |> FsProjPath.findExactlyOneFromProjectDir
        let projectName = projectPath |> ProjectName.fromFsProjPath

        log.Debug("Project file: {}", FsProjPath.asString projectPath)
        log.Debug("Project name: {}", ProjectName.asString projectName)

        let routeFileParts =
            url
            |> String.split "/"
            |> Array.map (fun x -> $"%s{x[0..0].ToUpper()}%s{x[1..]}")
            |> fun x -> [ "src"; "Pages"; yield! x; "Page.fs" ]

        let routeFilePath =
            absoluteProjectDir
            |> AbsoluteProjectDir.asFilePath
            |> FilePath.appendParts routeFileParts

        log.Debug("routeFilePath: {}", routeFilePath)

        let! settings = getSettings absoluteProjectDir

        let route =
            fileToRoute projectName absoluteProjectDir settings.PageSettings routeFilePath

        let! projectPath = absoluteProjectDir |> FsProjPath.findExactlyOneFromProjectDir
        let rootModuleName = projectName |> ProjectName.asString |> wrapWithTicksIfNeeded

        let writeResourceToProjectDir =
            writeResource (AbsoluteProjectDir.asFilePath absoluteProjectDir) false

        do!
            writeResourceToProjectDir
                "AddPage.template"
                routeFileParts
                (Some(
                    handlebars {|
                        ViewType = settings.ViewType
                        RootModule = rootModuleName
                        Route = route
                    |}
                ))

        let! routeData = getTemplateData projectName absoluteProjectDir
        log.Debug("routeData: {}", routeData)
        do! generateFiles (AbsoluteProjectDir.asFilePath absoluteProjectDir) routeData

        let relativefilePathString = $"""%s{routeFileParts |> String.concat "/"}"""

        $"""%s{getAppTitle ()} added a new page at %s{url}
You can edit your new page here:
./%s{relativefilePathString}

Please add the file to the project using an IDE or add the following line to an
ItemGroup in the project file '%s{FsProjPath.asString projectPath}':
<Compile Include="%s{relativefilePathString}" />
"""
        |> log.Info
    }
