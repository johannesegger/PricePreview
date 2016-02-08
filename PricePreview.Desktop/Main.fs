module PricePreview.Desktop.Main

open System
open FsXaml

type App = XAML<"App.xaml">

[<STAThread>]
[<EntryPoint>]
let main args =
    App().Root.Run()
