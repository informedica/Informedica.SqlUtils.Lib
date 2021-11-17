namespace Informedica.SqlUtils.Lib.Tests

open System
open Expecto
open Informedica.SqlUtils.Lib

module SayTests =
    [<Tests>]
    let tests =
        testList "samples" [
            testCase "Add two integers" <| fun _ ->
                Expect.equal 3 3 "Addition works"
        ]
