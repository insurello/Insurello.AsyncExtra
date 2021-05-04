namespace Insurello.AsyncExtra

[<RequireQualifiedAccessAttribute>]
module Async =
    let singleton : 'value -> Async<'value> = async.Return

    let bind : ('x -> Async<'y>) -> Async<'x> -> Async<'y> = fun f x -> async.Bind(x, f)

    let map : ('x -> 'y) -> Async<'x> -> Async<'y> = fun f x -> bind (f >> singleton) x

type AsyncResult<'x, 'err> = Async<Result<'x, 'err>>

[<RequireQualifiedAccessAttribute>]
module AsyncResult =
    let fromResult : Result<'a, 'err> -> AsyncResult<'a, 'err> = Async.singleton

    let singleton : 'a -> AsyncResult<'a, 'err> = fun x -> fromResult (Ok x)

    let fromOption : 'err -> Option<'a> -> AsyncResult<'a, 'err> =
        fun err option ->
            match option with
            | Some x -> Ok x
            | None -> Error err
            |> fromResult

    let fromTask : (unit -> System.Threading.Tasks.Task<'a>) -> AsyncResult<'a, string> =
        fun lazyTask ->
            async.Delay(lazyTask >> Async.AwaitTask)
            |> Async.Catch
            |> Async.map
                (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let fromUnitTask : (unit -> System.Threading.Tasks.Task) -> AsyncResult<unit, string> =
        fun lazyTask ->
            async.Delay(lazyTask >> Async.AwaitTask)
            |> Async.Catch
            |> Async.map
                (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let map : ('a -> 'b) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> =
        fun f asyncResultX -> Async.map (Result.map f) asyncResultX

    let mapError : ('errX -> 'errY) -> AsyncResult<'a, 'errX> -> AsyncResult<'a, 'errY> =
        fun f asyncResultX -> Async.map (Result.mapError f) asyncResultX

    let bind : ('a -> AsyncResult<'b, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> =
        fun successMapper ->
            Async.bind
                (function
                | Ok x -> successMapper x
                | Error err -> Async.singleton (Error err))

    let bindError : ('errX -> AsyncResult<'a, 'errY>) -> AsyncResult<'a, 'errX> -> AsyncResult<'a, 'errY> =
        fun errorMapper ->
            Async.bind
                (function
                | Ok x -> Async.singleton (Ok x)
                | Error err -> errorMapper err)

    let apply : AsyncResult<('a -> 'b), 'err> -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> =
        fun fA xA ->
            async {
                let! fR = fA
                let! xR = xA

                return
                    match fR, xR with
                    | Ok f, Ok x -> Ok(f x)
                    | Error err1, Ok _ -> Error err1
                    | Ok _, Error err2 -> Error err2
                    | Error err1, Error _ -> Error err1
            }

    let traverse : ('a -> 'b) -> List<AsyncResult<'a, 'err>> -> AsyncResult<'b list, 'err> =
        fun transformer list ->
            let rec fold acc =
                function
                | [] -> acc |> List.rev |> singleton
                | xA :: xAs -> bind (fun x -> fold (transformer x :: acc) xAs) xA

            fold [] list

    let sequence : List<AsyncResult<'a, 'error>> -> AsyncResult<'a list, 'error> = fun list -> traverse id list

    let private (<!>) = map
    let private (<*>) = apply

    let map2 : ('a -> 'b -> 'c) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> =
        fun f a1 a2 -> f <!> a1 <*> a2

    let map3 : ('a -> 'b -> 'c -> 'd) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> =
        fun f a1 a2 a3 -> f <!> a1 <*> a2 <*> a3

    let map4 : ('a -> 'b -> 'c -> 'd -> 'e) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> =
        fun f a1 a2 a3 a4 -> f <!> a1 <*> a2 <*> a3 <*> a4

    let map5 : ('a -> 'b -> 'c -> 'd -> 'e -> 'f) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> -> AsyncResult<'f, 'err> =
        fun f a1 a2 a3 a4 a5 -> f <!> a1 <*> a2 <*> a3 <*> a4 <*> a5

    let andMap : AsyncResult<'a, 'err> -> AsyncResult<('a -> 'b), 'err> -> AsyncResult<'b, 'err> =
        fun a1 a2 -> apply a2 a1

    let bind2 : ('a -> 'b -> AsyncResult<'c, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> =
        fun f a1 a2 -> map2 f a1 a2 |> bind id

    let bind3 : ('a -> 'b -> 'c -> AsyncResult<'d, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> =
        fun f a1 a2 a3 -> map3 f a1 a2 a3 |> bind id

    let bind4 : ('a -> 'b -> 'c -> 'd -> AsyncResult<'e, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> =
        fun f a1 a2 a3 a4 -> map4 f a1 a2 a3 a4 |> bind id

    let bind5 : ('a -> 'b -> 'c -> 'd -> 'e -> AsyncResult<'f, 'err>) -> AsyncResult<'a, 'err> -> AsyncResult<'b, 'err> -> AsyncResult<'c, 'err> -> AsyncResult<'d, 'err> -> AsyncResult<'e, 'err> -> AsyncResult<'f, 'err> =
        fun f a1 a2 a3 a4 a5 -> map5 f a1 a2 a3 a4 a5 |> bind id
