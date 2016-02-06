module Domain

type Product = {
    Name: string
    Amount: string
    PriceString: string
    Price: float option
    BasePrice: string option
}
