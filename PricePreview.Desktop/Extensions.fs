namespace PricePreview.Desktop

open System
open ReactiveUI
open Microsoft.FSharp.Linq.RuntimeHelpers

module ReactiveObjectExtensions =
    type ReactiveObject with
        member x.RaiseAndSetIfChanged(field, value, propertyQuotation) =
            match propertyQuotation with
            | Quotations.Patterns.PropertyGet(a, pi, list) ->
                x.RaiseAndSetIfChanged(field, value, pi.Name)
                |> ignore
            | _ -> failwith "Expected a property expression"

module Observable =
    open System.Linq.Expressions
    open Microsoft.FSharp.Quotations

    // http://stackoverflow.com/a/9135053/1293659
    let private toLinq (expr : Expr<'a -> 'b>) =
        let linq = LeafExpressionConverter.QuotationToExpression expr
        let call = linq :?> MethodCallExpression
        let lambda = call.Arguments.[0] :?> LambdaExpression
        Expression.Lambda<Func<'a, 'b>>(lambda.Body, lambda.Parameters) 

    let toProperty source propertyQuotation (obs: IObservable<'a>) =
        obs.ToProperty(source, toLinq propertyQuotation)

    let toPropertyWithInitialValue source propertyQuotation initialValue (obs: IObservable<'b>) =
        obs.ToProperty(source, toLinq propertyQuotation, initialValue, null)
