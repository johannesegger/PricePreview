[<AutoOpen>]
module PricePreview.Helper

open System.Text.RegularExpressions

// http://fssnip.net/29
let (|Regex|_|) pattern input =
    let m = Regex.Match(input, pattern)
    if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
    else None