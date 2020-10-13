namespace Insurello.AsyncExtra

[<RequireQualifiedAccessAttribute>]
module Async =
    let singleton: 'value -> Async<'value> = async.Return

    let bind: ('x -> Async<'y>) -> Async<'x> -> Async<'y> = fun f x -> async.Bind(x, f)

    let map: ('x -> 'y) -> Async<'x> -> Async<'y> = fun f x -> bind (f >> singleton) x

type AsyncResult<'x, 'err> = Async<Result<'x, 'err>>

[<RequireQualifiedAccessAttribute>]
module AsyncResult =
    let fromResult: Result<'x, 'err> -> AsyncResult<'x, 'err> = Async.singleton

    let fromOption: 'err -> Option<'x> -> AsyncResult<'x, 'err> =
        fun err option ->
            match option with
            | Some x -> Ok x
            | None -> Error err
            |> fromResult

    let fromTask: System.Threading.Tasks.Task<'x> -> AsyncResult<'x, string> =
        fun task ->
            task
            |> Async.AwaitTask
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let fromTeeTask: System.Threading.Tasks.Task -> AsyncResult<'x, string> =
        fun task ->
            task
            |> Async.AwaitTask
            |> Async.Catch
            |> Async.map (function
                | Choice1Of2 response -> Ok response
                | Choice2Of2 exn -> Error exn.Message)

    let map: ('x -> 'y) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun f asyncResultX -> Async.map (Result.map f) asyncResultX

    let mapError: ('errX -> 'errY) -> AsyncResult<'x, 'errX> -> AsyncResult<'x, 'errY> =
        fun f asyncResultX -> Async.map (Result.mapError f) asyncResultX

    let bind: ('x -> AsyncResult<'y, 'err>) -> AsyncResult<'x, 'err> -> AsyncResult<'y, 'err> =
        fun f asyncResultX ->
            asyncResultX
            |> Async.bind (function
                | Ok x -> f x

                | Error err -> Async.singleton (Error err))

    let sequence: AsyncResult<'x, 'error> list -> AsyncResult<'x list, 'error> =
        let folder: AsyncResult<'x list, 'error> -> AsyncResult<'x, 'error> -> AsyncResult<'x list, 'error> =
            fun acc nextAsyncResult ->
                acc |> bind (fun okValues -> nextAsyncResult |> map (fun nextOkValue -> nextOkValue :: okValues))

        fun asyncs ->
            asyncs
            |> List.fold folder (fromResult (Ok []))
            |> map List.rev
