module PricePreview.ProductCache

open System.IO
open Newtonsoft.Json
open Domain

let private getFilePath shopName =
    sprintf "%s.json" shopName

let private store shopName (products: Product list) =
    let json = products |> JsonConvert.SerializeObject
    File.WriteAllText(getFilePath shopName, json)

let private getCached shopName =
    File.ReadAllText(getFilePath shopName)
    |> fun x -> JsonConvert.DeserializeObject<Product list>(x)

let getCachedOrCalculate shopName calculateFn = async {
    try
        return getCached shopName
    with _ ->
        let! products = calculateFn()
        store shopName products
        return products
}
