namespace Insurello.AsyncExtra

[<RequireQualifiedAccess>]
module Async =
    let singleton: 'value -> Async<'value> = async.Return

    let bind: ('x -> Async<'y>) -> Async<'x> -> Async<'y> = fun f x -> async.Bind(x, f)

    let map: ('x -> 'y) -> Async<'x> -> Async<'y> = fun f x -> bind (f >> singleton) x

type AsyncResult<'x, 'err> = Async<Result<'x, 'err>>

[<RequireQualifiedAccess>]
module AsyncResult =
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

    let singleton: 'value -> AsyncResult<'value, 'e> = fun x -> Async.singleton (Ok x)

    let fromAsync: Async<'x> -> AsyncResult<'x, string> =
        fun async ->
            async
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let andMap: AsyncResult<'a, 'e> -> AsyncResult<('a -> 'b), 'e> -> AsyncResult<'b, 'e> =
        fun asyncResult f ->
            async {
                let! execF = Async.StartChild f

                let! execAsyncResult = Async.StartChild asyncResult

                let! f' = execF

                let! asyncResult' = execAsyncResult

                return match f', asyncResult' with
                       | Ok fOk, Ok aROk -> Ok(fOk aROk)
                       | Error e, _ -> Error e
                       | _, Error e -> Error e
            }

    let zip: AsyncResult<'a, 'e> -> AsyncResult<'b, 'e> -> AsyncResult<('a * 'b), 'e> =
        fun async1 async2 ->
            (fun a b -> a, b)
            |> singleton
            |> andMap async1
            |> andMap async2

[<AutoOpen>]
module AsyncResultCE =
    type AsyncResultBuilder() =
        member _.Return(value: 'a): AsyncResult<'a, 'e> = AsyncResult.singleton value

        member _.ReturnFrom(asyncResult: AsyncResult<'a, 'e>): AsyncResult<'a, 'e> = asyncResult

        member _.Zero(): AsyncResult<unit, 'e> = AsyncResult.singleton ()

        member _.Bind(asyncResult: AsyncResult<'a, 'e>, f: 'a -> AsyncResult<'b, 'e>): AsyncResult<'b, 'e> =
            AsyncResult.bind f asyncResult

        member _.Delay(f: unit -> AsyncResult<'a, 'e>): AsyncResult<'a, 'e> = async.Delay f

        member _.Combine(unitAsyncResult: AsyncResult<unit, 'e>, asyncResult: AsyncResult<'a, 'e>)
                         : AsyncResult<'a, 'e> =
            AsyncResult.bind (fun () -> asyncResult) unitAsyncResult

        member _.TryWith(asyncResult: AsyncResult<'a, 'e>, f: exn -> AsyncResult<'a, 'e>): AsyncResult<'a, 'e> =
            async.TryWith(asyncResult, f)

        member _.TryFinally(asyncResult: AsyncResult<'a, 'e>, f: unit -> unit): AsyncResult<'a, 'e> =
            async.TryFinally(asyncResult, f)

        member _.Using(disposable: 'a :> System.IDisposable, f: 'a -> AsyncResult<'b, 'e>): AsyncResult<'b, 'e> =
            async.Using(disposable, f)

        member _.BindReturn(asyncResult: AsyncResult<'a, 'e>, f: 'a -> 'b): AsyncResult<'b, 'e> =
            AsyncResult.map f asyncResult

        member _.MergeSources(asyncResult1: AsyncResult<'a, 'e>, asyncResult2: AsyncResult<'b, 'e>)
                              : AsyncResult<'a * 'b, 'e> =
            AsyncResult.zip asyncResult1 asyncResult2

        member inline _.Source(asyncResult: AsyncResult<'a, 'e>): AsyncResult<'a, 'e> = asyncResult

    let asyncResult = AsyncResultBuilder()

[<AutoOpen>]
module AsyncResultCEExtensions =
    type AsyncResultBuilder with
        member inline _.Source(asyncOp: Async<'a>): AsyncResult<'a, string> = AsyncResult.fromAsync asyncOp

        member inline _.Source(result: Result<'a, 'e>): AsyncResult<'a, 'e> = AsyncResult.fromResult result

        member inline _.Source(task: System.Threading.Tasks.Task<'a>): AsyncResult<'a, string> =
            AsyncResult.fromTask (fun () -> task)

        member inline _.Source(unitTask: System.Threading.Tasks.Task): AsyncResult<unit, string> =
            AsyncResult.fromUnitTask (fun () -> unitTask)
