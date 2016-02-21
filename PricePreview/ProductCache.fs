module PricePreview.ProductCache

open System.IO
open Newtonsoft.Json
open Domain

let private tryReadAllText fileName =
    try
        File.ReadAllText fileName |> Success
    with e -> Error [ e.ToString() ]

let private tryWriteAllText fileName content =
    try
        File.WriteAllText(fileName, content) |> Success
    with e -> Error [ e.ToString() ]

let private tryDeserializeObject<'a> json =
    try
        JsonConvert.DeserializeObject<'a>(json) |> Success
    with e -> Error [ e.ToString() ]

let private getFilePath (shopName: string) =
    shopName.ToLowerInvariant()
    |> sprintf "%s.json"

let private store shopName (products: Product list) =
    products
    |> JsonConvert.SerializeObject
    |> tryWriteAllText (getFilePath shopName)

let private getCached shopName =
    getFilePath shopName
    |> tryReadAllText
    |> Result.bind tryDeserializeObject<Product list>

let getCachedOrCalculate shopName calculateFn = async {
    return!
        getCached shopName
        |> function
        | Success x -> async { return Success x }
        | Error x -> async {
            let! products = calculateFn()
            return 
                products
                |> Result.bind (fun p ->
                    store shopName p
                    |> Result.map (fun () -> p)
                )
        }
}
