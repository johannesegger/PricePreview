module Http

open System.Net.Http
open System.Text
open HtmlAgilityPack

let get (url: string) = async {
    printfn "Fetching %s" url
    use client = new HttpClient()
    let! response = client.GetAsync url |> Async.AwaitTask
    let! content = response.Content.ReadAsStreamAsync() |> Async.AwaitTask
    let document = HtmlDocument()
    document.Load(content, Encoding.UTF8)
    return document
}