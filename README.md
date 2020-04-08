# FSharp.AsyncExtra

Library for handling Async operations that might fail. Mainly gives you the type `AsyncResult` and helper functions for creating and working with this type.

### Why does it exist?

We wanted to practice [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) in the Async world which is tricky in F# without helper functions.

A common pattern in the real world is that a service needs to parse input, call different services, do DB-lookups, do conversions, store and return something. These steps can usually fail in different ways. E.g. Invalid input, external request failed etc. In F# we have the `Result` type for things that might fail and `Async` for things that are asynchronous but we usually need the combination of these two, `AsyncResult`, which is not a type that exist in F#. This makes it tedious to work with and requires a lot of plumbing (see example below).

### Inspiration

This package is inspired by Scott Wlaschin's book - [Domain Modeling Made Functional](https://pragprog.com/book/swdddf/domain-modeling-made-functional) and the [associated code repository](https://github.com/swlaschin/DomainModelingMadeFunctional/blob/master/src/OrderTakingEvolved/Result.fs)

### AsyncResult type

The `AsyncResult` type is an alias

```fsharp
type AsyncResult<'success, 'error> = Async<Result<'success, 'error>>
```

### Example

```fsharp
module AsyncResultExample

open AsyncExtra

// SETUP EXAMPLE-FUNCTIONS
let fetchPersonIds: unit -> Async<List<int>> =
    fun () ->
        async {
            do! Async.Sleep(3000)
            return [ 31; 27; 92 ]
        }

let personName: string -> Async<Result<string, string>> =
    fun id ->
        async {
            do! Async.Sleep(3000)
            return match id with
                   | "31" -> Ok "Alice"
                   | "27" -> Ok "Bob"
                   | "92" -> Ok "Scott"
                   | _ -> Error "No person found"
        }

let firstId: List<int> -> Result<int, string> =
    fun ids ->
        ids
        |> List.tryHead
        |> function
        | Some id -> Ok id
        | None -> Error "Empty list of Ids"

// SETUP EXAMPLE-FUNCTIONS DONE

// Without AsyncExtra
let firstPersonsName: unit -> Async<Result<string, string>> =
    fun () ->
        async {
            let! personIds = fetchPersonIds()
            let id =
                personIds
                |> firstId
                |> Result.map string
            let! name = match id with
                        | Ok id -> personName id
                        | Error error -> async.Return(Error error)
            return name
        }

// With AsyncExtra
let firstPersonsNameWithAsyncResult: unit -> AsyncResult<string, string> =
    fun () ->
        fetchPersonIds()
        |> Async.map firstId
        |> AsyncResult.map string
        |> AsyncResult.bind personName


```

[![Insurello](https://gitcdn.xyz/repo/insurello/elm-swedish-bank-account-number/master/insurello.svg)](https://jobb.insurello.se/departments/product-tech)
