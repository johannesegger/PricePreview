module Http

open System
open System.Net.Http
open System.Text
open HtmlAgilityPack

let get (url: Uri) = async {
    printfn "Fetching %O" url
    use client = new HttpClient()
    try
        let! response = client.GetAsync url |> Async.AwaitTask
        let! content = response.Content.ReadAsStreamAsync() |> Async.AwaitTask
        let document = HtmlDocument()
        document.Load(content, Encoding.UTF8)
        return Success document
    with e -> return Error [ sprintf "Error while fetching %O: %O" url e ]
}