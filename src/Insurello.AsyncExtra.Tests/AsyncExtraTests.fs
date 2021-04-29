module AsyncExtraTests

open System.Threading
open System.Threading.Tasks
open Insurello.AsyncExtra
open Expecto

let toAsyncResult<'x> : 'x -> AsyncResult<'x, string> = Ok >> AsyncResult.fromResult

[<Tests>]
let sequenceTests =
    testList
        "Sequence tests"
        [ testAsync "should return values in same order as given tasks" {
            let dummyAsync : int -> AsyncResult<int, string> = Ok >> AsyncResult.fromResult

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

              let dummyAsync : int -> AsyncResult<int, string> =
                  fun i ->
                      AsyncResult.fromResult (Ok i)
                      |> AsyncResult.map
                          (fun j ->
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
let traverseTests =
    testList
        "Traverse tests"
        [ testAsync "should return values in same order as given tasks" {
            let expected = Ok [ 1; 2; 3 ]

            let input =
                [ (toAsyncResult 1)
                  (toAsyncResult 2)
                  (toAsyncResult 3) ]

            let! actual = AsyncResult.traverse id input
            Expect.equal actual expected "should equal"
          }
          testAsync "should return map the AsyncResult values" {
              let transformer = ((+) 10)

              let expected = Ok [ 11; 12; 13 ]

              let input =
                  [ (toAsyncResult 1)
                    (toAsyncResult 2)
                    (toAsyncResult 3) ]

              let! actual = AsyncResult.traverse transformer input
              Expect.equal actual expected "should equal"
          }
          testAsync "should execute async task in sequence" {
              let mutable orderRun = []

              let dummyAsync : int -> AsyncResult<int, string> =
                  fun i ->
                      AsyncResult.fromResult (Ok i)
                      |> AsyncResult.map
                          (fun j ->
                              orderRun <- List.append orderRun [ j ]
                              j)

              let input =
                  [ Async.Sleep 100
                    |> Async.bind (fun _ -> dummyAsync 1)
                    (dummyAsync 2)
                    (dummyAsync 3) ]

              let expectedOkValue = [ 1; 2; 3 ]
              let! _actual = AsyncResult.traverse id input
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
                let input : (unit -> Task) = fun () -> Task.Delay(0, source.Token)

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

[<Tests>]
let applyTest =
    testList
        "Test apply"
        [ testAsync "should follow the law of Identity (apply id v = v)" {
            let v = AsyncResult.fromResult (Ok 42)
            let f = AsyncResult.fromResult (Ok id)

            let! actual = AsyncResult.apply f v
            let! expectedValue = v

            Expect.equal actual expectedValue "Should be equal"
          }
          testAsync "should follow the law of Homomorphism (apply fA xA = AR.of (f x)" {
              let x = 42
              let f = ((+) 10)

              let xA = toAsyncResult x
              let fA = toAsyncResult f

              let! actual = AsyncResult.apply fA xA
              let! expectedValue = toAsyncResult (f x)

              Expect.equal actual expectedValue "Should be equal"
          }
          testAsync "should follow the law of Composition (xA |> apply fA1 |> apply fA2 = apply fA1 (apply fA2 xA))" {
              let fA1 = toAsyncResult ((+) 10)
              let fA2 = toAsyncResult ((-) 2)
              let xA = toAsyncResult 42

              let! actual =
                  xA
                  |> AsyncResult.apply fA2
                  |> AsyncResult.apply fA1

              let! expectedValue = AsyncResult.apply fA1 (AsyncResult.apply fA2 xA)

              Expect.equal actual expectedValue "Should be equal"
          } ]

[<Tests>]
let mapTests =
    testList
        "Test map functions"
        [ testList
            "map"
            [ testAsync "should change the value in an AsyncResult" {
                let input = toAsyncResult 3

                let expectedValue = Ok 4

                let! actual = AsyncResult.map ((+) 1) input

                Expect.equal actual expectedValue "Should be equal"
              }
              testAsync "should NOT change the value in an Error AsyncResult" {
                  let input = AsyncResult.fromResult (Error 3)

                  let expectedValue = Error 3

                  let! actual = AsyncResult.map ((+) 1) input

                  Expect.equal actual expectedValue "Should be equal"
              } ]

          testList
              "map2"
              [ testAsync "should map over the value from two AsyncResult" {
                  let input1 = toAsyncResult 3
                  let input2 = toAsyncResult 7

                  let expectedValue = Ok 10

                  let! actual = AsyncResult.map2 ((+)) input1 input2

                  Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the first AsyncResult is an error" {
                    let input1 = AsyncResult.fromResult (Error "Not Ok")
                    let input2 = toAsyncResult 3

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.map2 ((+)) input1 input2

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the second AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map2 ((+)) input1 input2

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should pass arguments in order" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 7

                    let expected = Ok(3, 7)

                    let! actual = AsyncResult.map2 (fun a b -> (a, b)) input1 input2

                    Expect.equal actual expected "Should be equal"

                } ]

          testList
              "map3"
              [ testAsync "should map over the value from three AsyncResult" {
                  let input1 = toAsyncResult 3
                  let input2 = toAsyncResult 7
                  let input3 = toAsyncResult 4

                  let expectedValue = Ok 14

                  let! actual = AsyncResult.map3 (fun a b c -> a + b + c) input1 input2 input3

                  Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the first AsyncResult is an error" {
                    let input1 = AsyncResult.fromResult (Error "Not Ok")
                    let input2 = toAsyncResult 3
                    let input3 = toAsyncResult 7

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.map3 (fun a b c -> a + b + c) input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the second AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let input3 = toAsyncResult 3

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map3 (fun a b c -> a + b + c) input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the third AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 = toAsyncResult 12

                    let input3 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map3 (fun a b c -> a + b + c) input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should pass arguments in order" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 7
                    let input3 = toAsyncResult 12

                    let expected = Ok(3, 7, 12)

                    let! actual = AsyncResult.map3 (fun a b c -> (a, b, c)) input1 input2 input3

                    Expect.equal actual expected "Should be equal"

                } ]

          testList
              "map4"
              [ testAsync "should map over the value from four AsyncResult" {
                  let input1 = toAsyncResult 3
                  let input2 = toAsyncResult 7
                  let input3 = toAsyncResult 4
                  let input4 = toAsyncResult 6

                  let expectedValue = Ok 20

                  let! actual = AsyncResult.map4 (fun a b c d -> a + b + c + d) input1 input2 input3 input4

                  Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the first AsyncResult is an error" {
                    let input1 = AsyncResult.fromResult (Error "Not Ok")
                    let input2 = toAsyncResult 3
                    let input3 = toAsyncResult 7
                    let input4 = toAsyncResult 12

                    let expectedValue = Error "Not Ok"
                    let! actual = AsyncResult.map4 (fun a b c d -> a + b + c + d) input1 input2 input3 input4

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the second AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let input3 = toAsyncResult 3
                    let input4 = toAsyncResult 5

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map4 (fun a b c d -> a + b + c + d) input1 input2 input3 input4

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the third AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 = toAsyncResult 12

                    let input3 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let input4 = toAsyncResult 5

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map4 (fun a b c d -> a + b + c + d) input1 input2 input3 input4

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should fail if the fourth AsyncResult is an error" {
                    let input1 = toAsyncResult 3

                    let input2 = toAsyncResult 12
                    let input3 = toAsyncResult 13

                    let input4 =
                        AsyncResult.fromResult (Error "Not Ok either")

                    let expectedValue = Error "Not Ok either"

                    let! actual = AsyncResult.map4 (fun a b c d -> a + b + c + d) input1 input2 input3 input4

                    Expect.equal actual expectedValue "Should be equal"
                }
                testAsync "should pass arguments in order" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 7
                    let input3 = toAsyncResult 12
                    let input4 = toAsyncResult 0

                    let expected = Ok(3, 7, 12, 0)

                    let! actual = AsyncResult.map4 (fun a b c d -> (a, b, c, d)) input1 input2 input3 input4

                    Expect.equal actual expected "Should be equal"

                } ]

          testList
              "andMap"
              [ testAsync "should apply AsyncResult value to AsyncResult function" {
                  let fA = toAsyncResult ((+) 10)
                  let xA = toAsyncResult 20

                  let expected = Ok 30

                  let! actual = AsyncResult.andMap xA fA

                  Expect.equal actual expected "Should be equal"
                }
                testAsync "should be pipeable" {
                    let fA = toAsyncResult (fun a b c -> a + b + c)
                    let xA = toAsyncResult 10
                    let yA = toAsyncResult 20
                    let zA = toAsyncResult 30

                    let expected = Ok 60

                    let! actual =
                        fA
                        |> AsyncResult.andMap xA
                        |> AsyncResult.andMap yA
                        |> AsyncResult.andMap zA

                    Expect.equal actual expected "Should be equal"
                }
                testAsync "should fail if there is an Error" {
                    let fA = toAsyncResult (fun a b c -> a + b + c)
                    let xA = toAsyncResult 10

                    let yA =
                        AsyncResult.fromResult (Error "Oh no an error")

                    let zA = toAsyncResult 30

                    let expected = Error "Oh no an error"

                    let! actual =
                        fA
                        |> AsyncResult.andMap xA
                        |> AsyncResult.andMap yA
                        |> AsyncResult.andMap zA

                    Expect.equal actual expected "Should be equal"
                } ] ]

[<Tests>]
let bindTests =
    testList
        "Test bind functions"
        [ testList
            "bind"
            [ testAsync "should change the value in an AsyncResult" {
                let input = toAsyncResult 3

                let f = ((+) 1 >> toAsyncResult)

                let expectedValue = Ok 4

                let! actual = AsyncResult.bind f input

                Expect.equal actual expectedValue "Should be equal"
              }

              testAsync "should NOT change the value in an Error AsyncResult" {
                  let input = AsyncResult.fromResult (Error 3)

                  let f = (+) 1 >> Ok >> AsyncResult.fromResult

                  let expectedValue = Error 3

                  let! actual = AsyncResult.bind f input

                  Expect.equal actual expectedValue "Should be equal"
              } ]

          testList
              "bind2"
              [ testAsync "should map over the value from two AsyncResult" {
                  let input1 = toAsyncResult 3
                  let input2 = toAsyncResult 7
                  let f a b = toAsyncResult (a + b)

                  let expectedValue = Ok 10

                  let! actual = AsyncResult.bind2 f input1 input2

                  Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the first AsyncResult is an error" {
                    let input1 = AsyncResult.fromResult (Error "Not Ok")
                    let input2 = toAsyncResult 3
                    let f a b = toAsyncResult (a + b)

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.bind2 f input1 input2

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the second AsyncResult is an error" {
                    let input1 = toAsyncResult 3
                    let input2 = AsyncResult.fromResult (Error "Not Ok")
                    let f a b = toAsyncResult (a + b)

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.bind2 f input1 input2

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should pass arguments in order" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 7

                    let expected = Ok(3, 7)

                    let! actual = AsyncResult.bind2 (fun a b -> toAsyncResult (a, b)) input1 input2

                    Expect.equal actual expected "Should be equal"

                } ]

          testList
              "bind3"
              [ testAsync "should map over the value from three AsyncResult" {
                  let input1 = toAsyncResult 3
                  let input2 = toAsyncResult 7
                  let input3 = toAsyncResult 32
                  let f a b c = toAsyncResult (a + b + c)

                  let expectedValue = Ok 42

                  let! actual = AsyncResult.bind3 f input1 input2 input3

                  Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the first AsyncResult is an error" {
                    let input1 = AsyncResult.fromResult (Error "Not Ok")
                    let input2 = toAsyncResult 3
                    let input3 = toAsyncResult 32
                    let f a b c = toAsyncResult (a + b + c)

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.bind3 f input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the second AsyncResult is an error" {
                    let input1 = toAsyncResult 3
                    let input2 = AsyncResult.fromResult (Error "Not Ok")
                    let input3 = toAsyncResult 4
                    let f a b c = toAsyncResult (a + b + c)

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.bind3 f input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should fail if the third AsyncResult is an error" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 4
                    let input3 = AsyncResult.fromResult (Error "Not Ok")
                    let f a b c = toAsyncResult (a + b + c)

                    let expectedValue = Error "Not Ok"

                    let! actual = AsyncResult.bind3 f input1 input2 input3

                    Expect.equal actual expectedValue "Should be equal"
                }

                testAsync "should pass arguments in order" {
                    let input1 = toAsyncResult 3
                    let input2 = toAsyncResult 7
                    let input3 = toAsyncResult 99

                    let expected = Ok(3, 7, 99)

                    let! actual = AsyncResult.bind3 (fun a b c -> toAsyncResult (a, b, c)) input1 input2 input3

                    Expect.equal actual expected "Should be equal"

                } ] ]
