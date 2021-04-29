namespace Insurello.AsyncExtra

[<RequireQualifiedAccessAttribute>]
module Async =
    let singleton : 'value -> Async<'value> = async.Return

    let bind : ('x -> Async<'y>) -> Async<'x> -> Async<'y> = fun f x -> async.Bind(x, f)

    let map : ('x -> 'y) -> Async<'x> -> Async<'y> = fun f x -> bind (f >> singleton) x

type AsyncResult<'x, 'err> = Async<Result<'x, 'err>>

[<RequireQualifiedAccessAttribute>]
module AsyncResult =
    let fromResult : Result<'x, 'err> -> AsyncResult<'x, 'err> = Async.singleton

    let fromOption : 'err -> Option<'x> -> AsyncResult<'x, 'err> =
        fun err option ->
            match option with
            | Some x -> Ok x
            | None -> Error err
            |> fromResult

    let fromTask : (unit -> System.Threading.Tasks.Task<'x>) -> AsyncResult<'x, string> =
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

    let map : ('x -> 'y) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun f asyncResultX -> Async.map (Result.map f) asyncResultX

    let mapError : ('errX -> 'errY) -> AsyncResult<'x, 'errX> -> AsyncResult<'x, 'errY> =
        fun f asyncResultX -> Async.map (Result.mapError f) asyncResultX

    let bind : ('x -> AsyncResult<'y, 'err>) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun successMapper ->
            Async.bind
                (function
                | Ok x -> successMapper x
                | Error err -> Async.singleton (Error err))

    let bindError : ('errT -> AsyncResult<'x, 'errU>) -> AsyncResult<'x, 'errT> -> AsyncResult<'x, 'errU> =
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
            let cons head tail = head :: tail

            let mapConcat headR tailR =
                apply (map (transformer >> cons) headR) tailR

            List.foldBack mapConcat list (fromResult (Ok []))

    let sequence : AsyncResult<'x, 'error> list -> AsyncResult<'x list, 'error> = fun list -> traverse id list

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
