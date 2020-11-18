namespace Insurello.AsyncExtra

[<RequireQualifiedAccessAttribute>]
module Async =
    let singleton: 'value -> Async<'value> = async.Return

    let bind: ('x -> Async<'y>) -> Async<'x> -> Async<'y> = fun f x -> async.Bind(x, f)

    let map: ('x -> 'y) -> Async<'x> -> Async<'y> = fun f x -> bind (f >> singleton) x

type AsyncResult<'x, 'err> = Async<Result<'x, 'err>>

[<RequireQualifiedAccessAttribute>]
module AsyncResult =
    let singleton: 'x -> AsyncResult<'x, 'err> = fun x -> Async.singleton (Ok x)

    let fromResult: Result<'x, 'err> -> AsyncResult<'x, 'err> = Async.singleton

    let fromOption: 'err -> Option<'x> -> AsyncResult<'x, 'err> =
        fun err option ->
            match option with
            | Some x -> Ok x
            | None -> Error err
            |> fromResult

    let fromTask: (unit -> System.Threading.Tasks.Task<'x>) -> AsyncResult<'x, string> =
        fun lazyTask ->
            async.Delay(lazyTask >> Async.AwaitTask)
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let fromUnitTask: (unit -> System.Threading.Tasks.Task) -> AsyncResult<unit, string> =
        fun lazyTask ->
            async.Delay(lazyTask >> Async.AwaitTask)
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let map: ('x -> 'y) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun f asyncResultX -> Async.map (Result.map f) asyncResultX

    let mapError: ('errX -> 'errY) -> AsyncResult<'x, 'errX> -> AsyncResult<'x, 'errY> =
        fun f asyncResultX -> Async.map (Result.mapError f) asyncResultX

    let andMap: AsyncResult<'x, 'err> -> AsyncResult<('x -> 'y), 'err> -> AsyncResult<'y, 'err> =
        fun m f ->
            async {
                let! exec1 = Async.StartChild f

                let! exec2 = Async.StartChild m

                let! x1 = exec1

                let! x2 = exec2

                return match x1, x2 with
                       | Ok ok1, Ok ok2 -> Ok(ok1 ok2)
                       | Error e, _ -> Error e
                       | _, Error e -> Error e
            }

    let bind: ('x -> AsyncResult<'y, 'err>) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun successMapper ->
            Async.bind (function
                | Ok x -> successMapper x
                | Error err -> Async.singleton (Error err))

    let bindError: ('errT -> AsyncResult<'x, 'errU>) -> AsyncResult<'x, 'errT> -> AsyncResult<'x, 'errU> =
        fun errorMapper ->
            Async.bind (function
                | Ok x -> Async.singleton (Ok x)
                | Error err -> errorMapper err)

    let sequence: AsyncResult<'x, 'error> list -> AsyncResult<'x list, 'error> =
        let folder: AsyncResult<'x list, 'error> -> AsyncResult<'x, 'error> -> AsyncResult<'x list, 'error> =
            fun acc nextAsyncResult ->
                acc
                |> bind (fun okValues ->
                    nextAsyncResult
                    |> map (fun nextOkValue -> nextOkValue :: okValues))

        fun asyncs ->
            asyncs
            |> List.fold folder (fromResult (Ok []))
            |> map List.rev

    type AsyncResultBuilder() =
        member _.Return(x) = singleton x

        member _.ReturnFrom(m: AsyncResult<_, _>) = m

        member _.Bind(m, f) = bind f m

        member _.Bind((_, error), f) = bindError f error

        member _.Zero() = singleton ()
        
        member _.Combine(m1: AsyncResult<unit, 'err>, m2: AsyncResult<'x, 'err>) =
            bind (fun () -> m2) m1
        
        member _.MergeSources(m1: AsyncResult<'a, 'e>, m2: AsyncResult<'b, 'e>) =
            fun m1 m2 -> m1, m2
            |> singleton
            |> andMap m1
            |> andMap m2

        member _.Delay(generator: unit -> AsyncResult<_, _>) = async.Delay generator

    let asyncResult = AsyncResultBuilder()
