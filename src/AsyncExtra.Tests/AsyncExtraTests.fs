module AsyncExtraTests

open AsyncExtra
open Expecto

[<Tests>]
let tests =
    testList "Sequence tests"
        [ testAsync "should return values in same order as given tasks" {
              let dummyAsync: int -> AsyncResult<int, string> = Ok >> AsyncResult.fromResult

              let expected = Ok [ 1; 2; 3 ]

              let input =
                  [ (dummyAsync 1)
                    (dummyAsync 2)
                    (dummyAsync 3) ]
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
                  [ (dummyAsync 1)
                    (dummyAsync 2)
                    (dummyAsync 3) ]

              let expectedOkValue = [ 1; 2; 3 ]
              let! _actual = AsyncResult.sequence input
              Expect.equal orderRun expectedOkValue "Should be run in same order"
          } ]
