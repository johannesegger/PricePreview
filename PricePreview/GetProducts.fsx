#r "System.Net.Http"
#r "..\packages\HtmlAgilityPack\lib\Net45\HtmlAgilityPack.dll"

#load "Result.fs"
#load "Helper.fs"
#load "Domain.fs"
#load "Http.fs"
#load "Hofer.fs"

open PricePreview

Hofer.getProducts() |> Async.RunSynchronously
