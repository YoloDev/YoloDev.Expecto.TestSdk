module YoloDev.Expecto.TestSdk.Tests.Fixture

open System.Reflection
open Expecto

// Discovered and run by the adapter under test, not by this project's own runner (see Main.fs).
[<Tests>]
let tests =
  testList
    "fixture"
    [
      testCase "alpha" ignore
      testCase "beta" ignore
      testCase "gamma" ignore
      testCase "skipped" (fun () -> skiptest "skipped by the fixture")
    ]

let source = Assembly.GetExecutingAssembly().Location

let names = [ "fixture.alpha"; "fixture.beta"; "fixture.gamma"; "fixture.skipped" ]
