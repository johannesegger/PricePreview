module PricePreview.Hofer

open System
open System.Globalization
open System.Text.RegularExpressions
open HtmlAgilityPack
open Domain

let private tryParseFloat (text: string) =
    if String.IsNullOrEmpty text
    then Success 1.
    else
        text.Replace('\u2013', '0').Replace("-", "0")
        |> fun x -> Double.TryParse(x, NumberStyles.Float ||| NumberStyles.AllowThousands, CultureInfo.GetCultureInfo "de-AT")
        |> function
        | true, value -> Success value
        | _ -> Error [ sprintf "Couldn't parse \"%s\" as float" text ]

let private tryParsePrice (text: string) =
    let m = Regex.Match(text, "^(?<price>(?:\d+(?:,\d+)?)|(?:-,\d+))/(?<amount>\d+(?:\,\d+)?)?\s*(?<amountUnit>\w+)$")
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

let private tryParseAmount x =
    match x with
    | Regex "per (?:(\d+) )?kg" [ amount ] ->
        tryParseFloat amount
        |> Result.map ((*) 1000.)
        |> Result.map Grams
    | Regex "per (?:(\d+) )?g" [ amount ] ->
        tryParseFloat amount
        |> Result.map Grams
    | x -> Error [ sprintf "Couldn't parse \"%s\" as amount." x ]

let private tryParseProduct (productNode: HtmlNode) =
    let name = productNode.SelectSingleNode("h2[contains(@class,'box--description--header')]").InnerText.Trim()
    let defaultAmount =
        productNode.SelectSingleNode(".//span[contains(@class,'box--amount')]").InnerText.Trim()
        |> tryParseAmount
    let priceString =
        productNode.SelectNodes(".//span[contains(@class,'box--value') or contains(@class,'box--decimal')]")
        |> Seq.map (fun x -> x.InnerText.Trim())
        |> String.concat ""
    let price = tryParseFloat priceString
    let basePrice =
        productNode.SelectSingleNode(".//span[contains(@class,'box--baseprice')]")
        |> Option.ofObj
        |> Result.ofOption [ "No base price information" ]
        |> Result.map (fun n -> n.InnerText.Trim())
        |> Result.bind tryParsePrice
        |> Result.bindError (fun x ->
            Result.zip price defaultAmount
            |> Result.mapError (fun e -> x @ e)
        )

    let calculatedDefaultAmount =
        defaultAmount
        |> Result.bindError (fun x ->
            Result.zip price basePrice
            |> Result.map (fun (price, (basePrice, amount)) ->
                Amount.modify amount (fun value -> Math.Round(value * (price / basePrice)))
            )
            |> Result.mapError (fun e -> x @ e)
        )

    Result.zip basePrice calculatedDefaultAmount
    |> Result.map (fun ((price, amount), defaultAmount) ->
        { Name = name; Amount = amount; DefaultAmount = defaultAmount; Price = price }
    )

let private getCategoryProducts (overviewDoc: HtmlDocument) = async {
    let! products =
        overviewDoc.DocumentNode.SelectNodes("//nav[@id='main-nav']//div[contains(@class,'gm-bg-product-range')]//a[@href]")
        |> Seq.map (fun n -> n.Attributes.["href"].Value |> Uri)
        |> Seq.map (fun href -> async {
            let! productGroupPageResult = Http.get href
            return
                productGroupPageResult
                |> Result.map (fun productGroupPage ->
                    productGroupPage.DocumentNode.SelectNodes("//div[contains(@class,'box--description')]")
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
        })
        |> Async.Parallel
    return
        products
        |> Array.toList
        |> Result.sequenceA
        |> Result.map (List.collect id)
}

let getProducts() = async {
    let url = Uri "https://www.hofer.at/de/"
    let! startPageResult = Http.get url
    let! products =
        startPageResult
        |> Result.map getCategoryProducts
        |> Result.sequenceAsyncA
    return
        products
        |> Result.bind id
}
