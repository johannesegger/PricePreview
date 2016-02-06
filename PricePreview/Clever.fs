module Clever

open System
open System.Globalization
open Domain

let tryParsePrice (text: string) =
    Double.TryParse(text, NumberStyles.Float ||| NumberStyles.AllowThousands ||| NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo "de-AT")
    |> function
    | true, value -> Some value
    | _ -> None

let getProducts() = async {
    let! startPage = Http.get "http://www.cleverkaufen.at/clevere_Produkte/clever_Produkte/Produktgruppe/Produktauswahl/cl_ProductMainCategory.aspx"
    let! products =
        startPage.DocumentNode.SelectNodes("//div[contains(@class,'teaser') and contains(@class,'cat')]//a[@href]")
        |> Seq.map (fun n -> n.Attributes.["href"].Value)
        |> Seq.map (fun href -> async {
            let! productGroupPage = Http.get href
            return!
                productGroupPage.DocumentNode.SelectNodes("//div[contains(@class,'teaser') and contains(@class,'cat')]//a[@href]")
                |> Seq.map (fun n -> n.Attributes.["href"].Value)
                |> Seq.map (fun href -> async {
                    let! productSubGroupPage = Http.get href
                    return
                        productSubGroupPage.DocumentNode.SelectNodes("//div[contains(@class,'teaser') and contains(@class,'prod')]")
                        |> Seq.map (fun n ->
                            let name = n.SelectSingleNode(".//a[contains(@class,'name')]/span[1]").InnerText.Trim()
                            let amount = n.SelectSingleNode(".//span[contains(@class,'amount')]").InnerText.Trim()
                            let priceString = n.SelectSingleNode(".//span[contains(@class,'price')]").InnerText.Trim()
                            let price = tryParsePrice priceString
                            { Name = name; Amount = amount; PriceString = priceString; Price = price; BasePrice = None }
                        )
                })
                |> Async.Parallel
        })
        |> Async.Parallel
    return
        products
        |> Seq.collect id
        |> Seq.collect id
        |> Seq.toList
}