module Billa

open System
open System.Globalization

module Http =
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

let tryParsePrice (text: string) =
    text.Replace('\u2013', '0')
    |> fun x -> Double.TryParse(x, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.GetCultureInfo "de-AT")
    |> function
    | true, value -> Some value
    | _ -> None

type Product = {
    Name: string
    Amount: string
    PriceString: string
    Price: float option
    BasePrice: string option
}

let getProducts() = async {
    let! startPage = Http.get "https://www.hofer.at/de/"
    let! products =
        startPage.DocumentNode.SelectNodes("//nav[@id='main-nav']//div[contains(@class,'gm-bg-product-range')]//a[@href]")
        |> Seq.map (fun n -> n.Attributes.["href"].Value)
        |> Seq.map (fun href -> async {
            let! productGroupPage = Http.get href
            return
                productGroupPage.DocumentNode.SelectNodes("//div[contains(@class,'box--description')]")
                |> function
                | null -> Seq.empty
                | items ->
                    items
                    |> Seq.map (fun n ->
                        let name = n.SelectSingleNode("h2[contains(@class,'box--description--header')]").InnerText.Trim()
                        let amount = n.SelectSingleNode(".//span[contains(@class,'box--amount')]").InnerText.Trim()
                        let priceString =
                            n.SelectNodes(".//span[contains(@class,'box--value') or contains(@class,'box--decimal')]")
                            |> Seq.map (fun x -> x.InnerText.Trim())
                            |> String.concat ""
                        let price = tryParsePrice priceString
                        let basePrice =
                            n.SelectSingleNode(".//span[contains(@class,'box--baseprice')]")
                            |> function
                            | null -> None
                            | n -> n.InnerText.Trim() |> Some
                        { Name = name; Amount = amount; PriceString = priceString; Price = price; BasePrice = basePrice }
                    )
        })
        |> Async.Parallel
    return
        products
        |> Seq.collect id
        |> Seq.toList
}
