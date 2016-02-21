[<AutoOpen>]
module Result_

type Result<'a, 'b> =
    | Success of 'a
    | Error of 'b list

module Result =
    let bind fn =
        function
        | Success x -> fn x
        | Error x -> Error x

    let bindError fn =
        function
        | Success x -> Success x
        | Error x -> fn x

    let map fn =
        bind (fn >> Success)

    let mapError fn =
        bindError (fn >> Error)

    let perform fn =
        function
        | Success x -> fn x; Success x
        | Error x -> Error x

    let ofOption error =
        function
        | Some x -> Success x
        | None -> Error error

    let partitionList list =
        list |> List.choose (function | Success x -> Some x | Error _ -> None)
        , list |> List.choose (function | Success _ -> None | Error x -> Some x)

    let apply fn x =
        match fn, x with
        | Success fn, Success x -> fn x |> Success
        | Error x, Error y -> Error (x @ y)
        | Error fn, _ -> Error fn
        | _, Error x -> Error x

    let traverseA f list =
        let (<*>) = apply
        let cons head tail = head :: tail

        let initState = Success []
        let folder head tail = 
            Success cons <*> (f head) <*> tail

        List.foldBack folder list initState

    let sequenceA list = traverseA id list

    let traverseAsyncA f x =
        match x with
        | Success workflow -> async {
            let! result = workflow
            return Success (f result)
            }
        | Error x -> async { return Error x }

    let sequenceAsyncA x = traverseAsyncA id x

    let chooseSuccess list =
        list
        |> List.choose (function | Success x -> Some x | Error _ -> None)

    let zip a b =
        match a, b with
        | Success x, Success y -> Success (x, y)
        | Error x, Error y -> Error (x @ y)
        | Error x, _ -> Error x
        | _, Error y -> Error y
    
    let zip3 a b c =
        zip b c
        |> zip a
        |> map (fun (a, (b, c)) -> a, b, c)
