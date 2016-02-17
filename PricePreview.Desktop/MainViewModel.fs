namespace PricePreview.Desktop

open System
open FsXaml
open ReactiveUI
open Domain
open ReactiveObjectExtensions
open FSharp.Control.Reactive

type UIProduct(product) as self =
    inherit ReactiveObject()

    let (amount, unit) = Amount.stringify product.Amount
    let amountRef = ref 0.
    let obsPrice =
        self.WhenAnyValue(fun x -> x.Amount)
        |> Observable.map (fun x -> x / amount * product.Price)
        |> Observable.toProperty self <@ fun (x: UIProduct) -> x.Price @>

    member x.Name = product.Name
    member x.Amount
        with get() = amountRef.Value
        and set value = self.RaiseAndSetIfChanged(amountRef, value, <@@ self.Amount @@>)
    member x.Unit = unit
    member x.Price = obsPrice.Value

type Shop(name: string, products: Product list, productFilter) as self =
    inherit ReactiveObject()

    let uiProducts =
        products
        |> List.sortBy (fun x -> x.Name)
        |> List.map UIProduct

    let obsProducts =
        productFilter
        |> Observable.map (fun searchText ->
            uiProducts
            |> List.filter (fun x -> x.Name.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) > -1)
        )
        |> Observable.toPropertyWithInitialValue self <@ fun (x: Shop) -> x.Products @> []

    let obsTotalPrice =
        uiProducts
        |> List.map (fun p ->
            p.WhenAnyValue(fun x -> x.Price)
        )
        |> Observable.combineLatestSeq
        |> Observable.map (fun l -> Seq.sum l)
        |> Observable.toProperty self <@ fun (x: Shop) -> x.TotalPrice @>

    member x.Name = name
    member x.Products = obsProducts.Value
    member x.TotalPrice = obsTotalPrice.Value

type MainViewModel() as self = 
    inherit ReactiveObject()
    
    let searchText = ref ""
    let unimarktProducts =
        PricePreview.ProductCache.getCachedOrCalculate "unimarkt" Unimarkt.getProducts
        |> Async.RunSynchronously
    let shops = [
        Shop("Unimarkt", unimarktProducts, self.WhenAnyValue(fun x -> x.SearchText))
    ]

    member x.SearchText
        with get() = searchText.Value
        and set value = self.RaiseAndSetIfChanged(searchText, value, <@@ self.SearchText @@>)
    member x.Shops = shops
