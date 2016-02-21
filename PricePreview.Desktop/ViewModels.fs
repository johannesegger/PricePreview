namespace PricePreview.Desktop.ViewModels

open System
open FsXaml
open ReactiveUI
open Domain
open PricePreview
open PricePreview.Desktop
open ReactiveObjectExtensions
open FSharp.Control.Reactive
open System.Reactive.Concurrency

type UIProduct(product) as self =
    inherit ReactiveObject()

    let isChecked = ref false
    let (amount, unit) = Amount.stringify product.Amount
    let amountRef = ref (Amount.stringify product.DefaultAmount |> fst)
    let obsPrice =
        self.WhenAnyValue(fun x -> x.Amount)
        |> Observable.map (fun x -> x / amount * product.Price)
        |> Observable.toProperty self <@ fun (x: UIProduct) -> x.Price @>

    member x.IsChecked
        with get() = isChecked.Value
        and set value = self.RaiseAndSetIfChanged(isChecked, value, <@@ self.IsChecked @@>)
    member x.Name = product.Name
    member x.Amount
        with get() = amountRef.Value
        and set value = self.RaiseAndSetIfChanged(amountRef, value, <@@ self.Amount @@>)
    member x.Unit = unit
    member x.Price = obsPrice.Value

type Shop(name: string, products: Product list, showAllProductsObs, searchTextObs) as self =
    inherit ReactiveObject()

    let uiProducts =
        products
        |> List.sortBy (fun x -> x.Name)
        |> List.map UIProduct

    let obsProducts =
        Observable.combineLatest (fun x y -> x, y) searchTextObs showAllProductsObs
        |> Observable.map (fun ((searchText: string), showAllProducts) ->
            uiProducts
            |> List.filter (fun x ->
                searchText.Split(' ')
                |> Array.forall(fun word -> x.Name.IndexOf(word, StringComparison.CurrentCultureIgnoreCase) > -1)
            )
            |> List.filter (fun x -> showAllProducts || x.IsChecked)
        )
        |> Observable.toPropertyWithInitialValue self <@ fun (x: Shop) -> x.Products @> []

    let obsTotalPrice =
        uiProducts
        |> List.map (fun p ->
            Observable.combineLatest
                (fun isChecked price -> if isChecked then price else 0.0)
                (p.WhenAnyValue(fun x -> x.IsChecked))
                (p.WhenAnyValue(fun x -> x.Price))
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
    let showAllProducts = ref true

    let showAllProductsObservable = self.WhenAnyValue(fun x -> x.ShowAllProducts)
    let searchTextObservable =
        self.WhenAnyValue(fun x -> x.SearchText)
        |> Observable.throttle (TimeSpan.FromMilliseconds 500.)
        |> Observable.observeOn Scheduler.CurrentThread
    
    let shops = [
        "Unimarkt", Unimarkt.getProducts
        "Hofer", Hofer.getProducts
        "Clever", Clever.getProducts
    ]

    let uiShops =
        shops
        |> List.map (fun (name, calculateFn) -> name, ProductCache.getCachedOrCalculate name calculateFn |> Async.RunSynchronously)
        |> List.choose (fun (name, products) -> match products with | Success x -> Some (name, x) | x -> None)
        |> List.map (fun (name, products) -> Shop(name, products, showAllProductsObservable, searchTextObservable))

    member x.SearchText
        with get() = searchText.Value
        and set value = self.RaiseAndSetIfChanged(searchText, value, <@@ self.SearchText @@>)

    member x.ShowAllProducts
        with get() = showAllProducts.Value
        and set value = self.RaiseAndSetIfChanged(showAllProducts, value, <@@ self.ShowAllProducts @@>)

    member x.Shops = uiShops
