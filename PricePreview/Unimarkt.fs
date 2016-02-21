module PricePreview.Unimarkt

open System
open System.Globalization
open System.Text.RegularExpressions
open HtmlAgilityPack
open Domain
open Http

let private tryParseFloat text =
    let culture = CultureInfo.GetCultureInfo "en-US"
    let (couldParseAmount, amount) = Double.TryParse(text, NumberStyles.Float, culture)
    if couldParseAmount then Success amount
    else Error [ sprintf "Couldn't parse \"%s\" as float" text ]

let private amountPattern = @"(?<amount>\d+(?:\.\d+)?)\s*(?<amountUnit>\w+)"

let private tryParsePrice (text: string) =
    let m = Regex.Match(text, "^&euro;\s*(?<price>\d+(?:\.\d+)?)\s*/\s*" + amountPattern)
    if m.Success
    then
        tryParseFloat m.Groups.["amount"].Value
        |> Result.bind (function | 0. -> Error [ "Reference amount is zero" ] | x -> Success x)
        |> Result.bind (fun amount ->
            tryParseFloat m.Groups.["price"].Value
            |> Result.bind (fun price ->
                getAmount amount m.Groups.["amountUnit"].Value
                |> Result.map (fun amount ->
                    price, amount
                )
            )
        )
    else
        Error [ "Incorrect format" ]
    |> Result.mapError (fun x -> [ sprintf "Couldn't parse \"%s\" as price: %s" text (String.concat "; " x) ])

let private tryParseAmount text =
    let m = Regex.Match(text, "^" + amountPattern)
    if m.Success
    then
        tryParseFloat m.Groups.["amount"].Value
        |> Result.bind (fun amount ->
            getAmount amount m.Groups.["amountUnit"].Value
        )
    else
        Error [ "Incorrect format" ]
    |> Result.mapError (fun x -> [ sprintf "Couldn't parse \"%s\" as amount: %s" text (String.concat "; " x) ])

let private tryParseProduct (productNode: HtmlNode) =
    let desc = productNode.SelectSingleNode("div[contains(@class,'desc')]")
    let name = desc.SelectSingleNode("strong").InnerText.Trim()
    let producer = desc.ChildNodes.[3].InnerText.Trim()
    let fullName = sprintf "%s - %s" name producer
    let defaultAmountText = desc.ChildNodes.[5].InnerText.Trim()

    Error []
    |> Result.bindError (fun _ ->
        productNode.SelectSingleNode(".//div[contains(@class,'vergleichspreis')]").InnerText.Trim()
        |> tryParsePrice
    )
    |> Result.bindError (fun _ ->
        let priceString = productNode.SelectSingleNode(".//span[contains(@class,'actualprice')]").InnerText.Trim()
        sprintf "%s / %s" priceString defaultAmountText
        |> tryParsePrice
    )
    |> Result.bind (fun (price, amount) ->
        defaultAmountText
        |> tryParseAmount
        |> Result.map (fun defaultAmount -> price, amount, defaultAmount)
    )
    |> Result.map (fun (price, amount, defaultAmount) ->
        { Name = fullName; Amount = amount; DefaultAmount = defaultAmount; Price = price }
    )
    |> Result.mapError (fun x -> [ sprintf "Couldn't parse product \"%s\": %s" name (String.concat "; " x) ])

let private getCategoryProducts url = async {
    let! productsPageResult = Http.get url
    return
        productsPageResult
        |> Result.map (fun productsPage ->
            productsPage.DocumentNode.SelectNodes("//div[contains(@class,'produktContainer')]")
            |> Option.ofObj
            |> Option.map (fun items ->
                items
                |> Seq.map tryParseProduct
                |> Seq.toList
                |> Result.chooseSuccess
            )
            |> Option.toList
            |> List.collect id
        )
}

let private getCategoriesProducts baseUrl (overviewDoc: HtmlDocument) = async {
    let! pageProductResults =
        overviewDoc.DocumentNode.SelectNodes("//div[@id='allCategoriesContainer']//li[contains(@class,'headline')]/a[@href]")
        |> Seq.map (fun n -> Uri(baseUrl, n.Attributes.["href"].Value))
        |> Seq.map getCategoryProducts
        |> Async.Parallel
    return
        pageProductResults
        |> Array.toList
        |> Result.sequenceA
        |> Result.map (List.collect id)
}

let getProducts() = async {
    let url = Uri "http://shop.unimarkt.at/alle-produkte"
    let! startPageResult = Http.get url
    let! products =
        startPageResult
        |> Result.map (getCategoriesProducts url)
        |> Result.sequenceAsyncA
    return
        products
        |> Result.bind id
}
