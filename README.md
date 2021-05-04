# Insurello.AsyncExtra

Library for handling Async operations that might fail. Mainly gives you the type `AsyncResult` and helper functions for creating and working with this type.

### Why does it exist?

We wanted to practice [Railway Oriented Programming](https://fsharpforfunandprofit.com/rop/) in the Async world which is tricky in F# without helper functions.

A common pattern in the real world is that a service needs to parse input, call different services, do DB-lookups, do conversions, store and return something. These steps can usually fail in different ways. E.g. Invalid input, external request failed, etc. In F# we have the `Result` type for things that might fail and `Async` for things that are asynchronous but we usually need the combination of these two, `AsyncResult`, which is not a type that exists in F#. This makes it tedious to work with and requires a lot of plumbing (see example below).

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

open Insurello.AsyncExtra

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

### Function documentation

The functions need to be prefixed with `AsyncResult.`.

#### Construction

##### singleton

```fsharp
singleton : 'a -> AsyncResult<'a, 'err>
```

The easiest way to create a new `AsyncResult` with an `Ok` value.

##### fromResult

```fsharp
fromResult : Result<'a, 'err> -> AsyncResult<'a, 'err>
```

Convert a `Result` into a `AsyncResult`.

##### fromOption

```fsharp
fromOption : 'err -> Option<'a> -> AsyncResult<'a, 'err>
```

Convert an `Option` into an `AsyncResult`. The first argument is the `Error` value that should be used if the option is `None`.

##### fromTask

```fsharp
fromTask : (unit -> System.Threading.Tasks.Task<'a>) -> AsyncResult<'a, string>
```

Convert a `Task` with a value into a `AsyncResult`.

##### fromUnitTask

```fsharp
fromUnitTask : (unit -> System.Threading.Tasks.Task) -> AsyncResult<unit, string>
```

Convert a unit `Task` into a `AsyncResult`.

#### Transformation

##### map

```fsharp
map : ('a -> 'b) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err>
```

You will most likely want to change the value in the `AsyncResult` by running functions. This is where `map` comes in handy. You send in a transformation function that will transform the value and `map` will then take the value from your `AsyncResult` run the function and return a brand new `AsyncResult` containing the new value. Worth keeping in mind is that the transformation function will only run if there is an `Ok` `AsyncResult`. If the `AsyncResult` contains an `Error` nothing will happen.

If the transform function will return an `AsyncResult` you might want to use [`bind`](#bind) instead.

```fsharp
let increase x = x + 1
let xA = AsyncResult.singleton 2
let eA = AsyncResult.fromResult (Error 2)

AsyncResult.map increase xA // Async<Ok 3>
AsyncResult.map increase eA // Async<Error 2>
```

##### mapError

```fsharp
mapError : ('errX -> 'errY) -> AsyncResult<'a, 'errX> -> AsyncResult<'a, 'errY>
```

Similar to [`map`](#map) but will instead apply the transformation function to the `Error`.

##### bind

```fsharp
bind : ('a -> AsyncResult<'b, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err>
```

Sometimes you want to use a transform function that will return an `AsyncResult` but you don't want to end up with a nested `AsyncResult<AsyncResult<'b, 'err>, 'err>`. This is where `bind` shines. It will transform the value, in the same way as [`map`](#map), but will flatten the returning `AsyncResult` and will return an `AsyncResult<'b, 'err>`. `bind` is also known as `andThen` in Elm and `chain` in JavaScript.

```fsharp
fetchUser : int -> AsyncResult<User, string>
fetchFriends : User -> AsyncResult<string list, string>

fetchUser 1 // Async<Ok { name: "Mary"; friends: [2; 3] }>
|> AsyncResult.bind fetchFriends // Async<Ok ["Peter"; "Paul"]>

fetchUser 5 // Async<Error "Can't find a user with that id">
|> AsyncResult.bind fetchFriends // Will never run

fetchUser 4 // Async<Ok { name: "The Grinch"; friends: [] }>
|> AsyncResult.bind fetchFriends // Async<Error "Can't find any friends">
```

##### bindError

```fsharp
bindError : ('errX -> AsyncResult<'a, 'errY>) -> AsyncResult<'a, 'errX> -> AsyncResult<'a, 'errY>
```

Similar to [`bind`](#bind) but will instead apply the AsyncResult returning transformation function to the `Error`.

##### apply

```fsharp
apply : AsyncResult<('a -> 'b), 'err> -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err>
```

Is used to apply a `AsyncResult` value to a function in an `AsyncResult`.

When either `AsyncResult` is `Error`, apply will return a new `Error` instance containing the `Error` value. This can be used to safely combine multiple values under a given combination function. If any of the inputs result in an `Error` then the computation will return an `Error` `AsyncResult`.

##### map2

```fsharp
map2 : ('a -> 'b -> 'c) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err>
```

Sometimes you want to transform the result of two `AsyncResult`. This is were `map2` comes into play. It requires a transformation function that is expecting two arguments. The order of the arguments are determined by the order of the two `AsyncResult`. The first value is applied first and the second value is applied as the second argument.

```fsharp
let add x y = x + y

let xA = AsyncResult.singleton 3
let yA = AsyncResult.singleton 4

AsyncResult.map2 add xA yA // Async<Ok 7>
```

##### map3

```fsharp
map3 : ('a -> 'b -> 'c -> 'd) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err>
```

Solves the same problem as [`map2`](#map2) but for three arguments.

##### map4

```fsharp
map4 : ('a -> 'b -> 'c -> 'd -> 'e) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err>
```

Solves the same problem as [`map2`](#map2) but for four arguments.

##### map5

```fsharp
map5 : ('a -> 'b -> 'c -> 'd -> 'e -> 'f) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> -> AsyncResult<'f, 'err>
```

Solves the same problem as [`map2`](#map2) but for five arguments.

##### andMap

```fsharp
andMap : AsyncResult<'a, 'err> -> AsyncResult<('a -> 'b), 'err> -> AsyncResult<'b, 'err>
```

In most cases `map2`-`map5` should be enough but in those cases you want to apply more arguments you can use `andMap`. Technically, `andMap` is the same as [`apply`](#apply) but the order of the arguments are reversed. While `apply` takes the function first and then the value, `andMap` takes the value first and then the function. This allow you to have a similar structure to your code as you would have using `mapX`.

```fsharp
let add3 = AsyncResult.singleton (fun a b c -> a + b + c)

let xA = AsyncResult.singleton 10
let yA = AsyncResult.singleton 20
let zA = AsyncResult.singleton 30

add3 // Async<Ok (fun a b c -> a + b + c)>
|> AsyncResult.andMap xA // Async<Ok (fun b c -> 10 + b + c)>
|> AsyncResult.andMap yA // Async<Ok (fun c -> 10 + 20 + c)>
|> AsyncResult.andMap zA // Async<Ok 60>
```

##### bind2

```fsharp
bind2 : ('a -> 'b -> AsyncResult<'c, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err>
```

You can use `bind2` to solve the same problem as [`map2`](#map2) if your transformation function returns an `AsyncResult`. In other words, their relationship is the same as `map` and `bind`.

##### bind3

```fsharp
bind3 : ('a -> 'b -> 'c -> AsyncResult<'d, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err>
```

Solves the same problem as [`bind2`](#bind2) but for three arguments.

##### bind4

```fsharp
bind4 : ('a -> 'b -> 'c -> 'd -> AsyncResult<'e, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err>
```

Solves the same problem as [`bind2`](#bind2) but for four arguments.

##### bind5

```fsharp
bind5 : ('a -> 'b -> 'c -> 'd -> 'e -> AsyncResult<'f, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> -> AsyncResult<'f, 'err>
```

Solves the same problem as [`bind2`](#bind2) but for five arguments.

#### Lists

##### sequence

```fsharp
sequence : List<AsyncResult<'a, 'error>> -> AsyncResult<'a list, 'error>
```

From time to time you will find yourself having a list of `AsyncResult` (`List<AsyncResult<'a, 'err>`) but you would rather have an `AsyncResult` with a list of values (`AsyncResult<'a list, 'error>`). In those cases `sequence` can help you. `sequence` will make an early return if it reaches an `Error`.

```fsharp
fetchUser : int -> AsyncResult<User, string>

userIds // [1; 2; 3]
|> List.map fetchUser // [Async<Ok User>; Async<Ok User>; Async<Ok User>]
|> AsyncResult.sequence // Async<Ok [User; User; User]>
```

##### traverse

```fsharp
traverse : ('a -> 'b) -> List<AsyncResult<'a, 'err>> -> AsyncResult<'b list, 'err>
```

Similar to [`sequence`](#sequence), `traverse` will also change the type from a list of `AsyncResult` to an `AsyncResult` with a list. The difference is that `traverse` allows you to use a transformation function to transform each value in the list of `AsyncResult`. `traverse` will make an early return if it reaches an `Error`.

By sending in `id` as the transform function you have implemented `sequence`. Let's have a look how we can solve the example in the `sequence` description using `traverse` instead.

```fsharp
fetchUser : int -> AsyncResult<User, string>

let userIds = [1; 2; 3]

AsyncResult.traverse fetchUser userIds // Async<Ok [User; User; User]>
```

[![Insurello](https://gitcdn.xyz/repo/insurello/elm-swedish-bank-account-number/master/insurello.svg)](https://jobb.insurello.se/departments/product-tech)
