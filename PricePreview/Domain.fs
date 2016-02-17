module Domain

type Amount =
    | Grams of float
    | Liters of float
with
    static member stringify =
        function
        | Grams x -> x, "g"
        | Liters x -> x * 1000., "ml"

type Product = {
    Name: string
    Amount: Amount
    Price: float
}

let getAmount amount =
    function
    | "g" -> Grams amount |> Some
    | "kg" -> Grams (amount * 1000.) |> Some
    | "ml" -> Liters (amount / 1000.) |> Some
    | "l" -> Liters amount |> Some
    | _ -> None
