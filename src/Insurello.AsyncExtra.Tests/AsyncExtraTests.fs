module AsyncExtraTests

open System.Threading
open System.Threading.Tasks
open Insurello.AsyncExtra
open Expecto

[<Tests>]
let tests =
    testList
        "Sequence tests"
        [ testAsync "should return values in same order as given tasks" {
              let sample = [ 1; 2; 3 ]

              let expected = Ok sample

              let input = List.map AsyncResult.singleton sample

              let! actual = AsyncResult.sequence input

              Expect.equal actual expected "should equal"
          }
          testAsync "should execute async task in sequence" {
              let mutable orderRun = []

              let dummyAsync: int -> AsyncResult<int, string> =
                  fun i ->
                      AsyncResult.fromResult (Ok i)
                      |> AsyncResult.map (fun j ->
                          orderRun <- List.append orderRun [ j ]
                          j)

              let input =
                  [ Async.Sleep 100
                    |> Async.bind (fun _ -> dummyAsync 1)
                    (dummyAsync 2)
                    (dummyAsync 3) ]

              let expectedOkValue = [ 1; 2; 3 ]
              let! _actual = AsyncResult.sequence input
              Expect.equal orderRun expectedOkValue "Should be run in same order"
          } ]

[<Tests>]
let taskTests =
    testList
        "Task tests"
        [ testList
            "fromUnitTask"
              [ testAsync "should convert from Task to AsyncResult" {
                    let source = new CancellationTokenSource()
                    let input: (unit -> Task) = fun () -> Task.Delay(0, source.Token)

                    let expectedValue = Ok()

                    let! actual = AsyncResult.fromUnitTask input

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "failing Task should result in Error" {
                    let source = new CancellationTokenSource()
                    let input = fun () -> Task.Delay(1000, source.Token)
                    let expectedValue = Error "A task was canceled."

                    source.Cancel()

                    let! actual = AsyncResult.fromUnitTask input

                    Expect.equal actual expectedValue "Should be equal"
                } ]
          testList
              "fromTask"
              [ testAsync "should convert from Task<string> to AsyncResult" {
                    let input =
                        fun () -> Async.singleton "Hello" |> Async.StartAsTask

                    let expectedValue = Ok "Hello"

                    let! actual = AsyncResult.fromTask input

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "fromTask failing Task should result in Error" {

                    let input =
                        fun () ->
                            Async.singleton "Hello"
                            |> Async.map (fun _ -> failwith "boom")
                            |> Async.StartAsTask

                    let expectedValue =
                        Error "One or more errors occurred. (boom)"

                    let! actual = AsyncResult.fromTask input

                    Expect.equal actual expectedValue "Should be equal"
                } ] ]
