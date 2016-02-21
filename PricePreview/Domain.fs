module Domain

type Amount =
    | Grams of float
    | Liters of float
    | Pieces of float
    | Mbe of float
    | Meters of float
with
    static member stringify =
        function
        | Grams x -> x, "g"
        | Liters x -> x * 1000., "ml"
        | Pieces x -> x, "Stk"
        | Mbe x -> x, "MBE"
        | Meters x -> x, "m"
    static member modify amount modifyFn =
        match amount with
        | Grams x -> modifyFn x |> Grams
        | Liters x -> modifyFn x |> Liters
        | Pieces x -> modifyFn x |> Pieces
        | Mbe x -> modifyFn x |> Mbe
        | Meters x -> modifyFn x |> Meters

type Product = {
    Name: string
    Amount: Amount
    DefaultAmount: Amount
    Price: float
}

let getAmount amount =
    function
    | "g" -> Grams amount |> Success
    | "kg" -> Grams (amount * 1000.) |> Success
    | "ml" -> Liters (amount / 1000.) |> Success
    | "l" -> Liters amount |> Success
    | "Stk" -> Pieces amount |> Success
    | "MBE" -> Mbe amount |> Success
    | "m" -> Meters amount |> Success
    | x -> Error [ sprintf "Invalid amount: %s" x ]
