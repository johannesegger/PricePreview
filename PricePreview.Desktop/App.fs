module PricePreview.Desktop.Main

open System
open System.Globalization
open System.Windows
open System.Windows.Markup
open FsXaml

type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main args =
    let app = App().Root
    use __ =
        app.Startup
        |> Observable.subscribe (fun _ ->
            FrameworkElement.LanguageProperty.OverrideMetadata
                (typeof<FrameworkElement>
                , new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("de-AT")))
                )
    app.Run()
