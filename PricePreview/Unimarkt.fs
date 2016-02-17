module Unimarkt

open System
open System.Globalization
open System.Text.RegularExpressions
open Domain
open Http

let private tryParseFloat text =
    let culture = CultureInfo.GetCultureInfo "en-US"
    let (couldParseAmount, amount) = Double.TryParse(text, NumberStyles.Float, culture)
    if couldParseAmount then Some amount
    else None

let private tryParsePrice (text: string) =
    let m = Regex.Match(text, "^&euro;\s*(?<price>\d+\.\d+)\s*/\s*(?<amount>\d+)\s*(?<amountUnit>\w+)$")
    if m.Success
    then
        tryParseFloat m.Groups.["amount"].Value
        |> Option.bind (function | 0. -> None | x -> Some x)
        |> Option.bind (fun amount ->
            tryParseFloat m.Groups.["price"].Value
            |> Option.bind (fun price ->
                getAmount amount m.Groups.["amountUnit"].Value
                |> Option.map (fun amount ->
                    price, amount
                )
            )
        )
    else None

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
                    |> Seq.choose (fun n ->
                        let desc = n.SelectSingleNode("div[contains(@class,'desc')]")
                        let name = desc.SelectSingleNode("strong").InnerText.Trim()
                        let producer = desc.ChildNodes.[3].InnerText.Trim()
                        let fullName = sprintf "%s - %s" name producer

                        let amount = desc.ChildNodes.[5].InnerText.Trim()
                        let priceString = n.SelectSingleNode(".//span[contains(@class,'actualprice')]").InnerText.Trim()

                        sprintf "%s / %s" priceString amount
                        |> tryParsePrice
                        |> Option.map (fun (price, amount) ->
                            { Name = fullName; Amount = amount; Price = price }
                        )
                    )
        })
        |> Async.Parallel
    return
        products
        |> Seq.collect id
        |> Seq.toList
}
