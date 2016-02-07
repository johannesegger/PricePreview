module Unimarkt

open System
open System.Globalization
open Domain
open Http

let private tryParsePrice (text: string) =
    text.Replace("&euro; ", "")
    |> fun x -> Double.TryParse(x, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.GetCultureInfo "en-US")
    |> function
    | true, value -> Some value
    | _ -> None

let getProducts() = async {
    let url = Uri "http://shop.unimarkt.at/alle-produkte"
    let! startPage = Http.get (url.ToString())
    let! products =
        startPage.DocumentNode.SelectNodes("//div[@id='allCategoriesContainer']//li[contains(@class,'headline')]/a[@href]")
        |> Seq.map (fun n -> n.Attributes.["href"].Value)
        |> Seq.map (fun href -> async {
            let! productsPage = Http.get (Uri(url, href).ToString())
            return
                productsPage.DocumentNode.SelectNodes("//div[contains(@class,'produktContainer')]")
                |> function
                | null -> Seq.empty
                | items ->
                    items
                    |> Seq.map (fun n ->
                        let desc = n.SelectSingleNode("div[contains(@class,'desc')]")
                        let name = desc.SelectSingleNode("strong").InnerText.Trim()
                        let producer = desc.ChildNodes.[3].InnerText.Trim()
                        let fullName = sprintf "%s - %s" name producer
                        let amount = desc.ChildNodes.[5].InnerText.Trim()
                        let priceString = n.SelectSingleNode(".//span[contains(@class,'actualprice')]").InnerText.Trim()
                        let price = tryParsePrice priceString
                        let basePrice = n.SelectSingleNode(".//div[contains(@class,'vergleichspreis')]").InnerText.Trim()
                        { Name = fullName; Amount = amount; PriceString = priceString; Price = price; BasePrice = Some basePrice }
                    )
        })
        |> Async.Parallel
    return
        products
        |> Seq.collect id
        |> Seq.toList
}
