module YoloDev.Expecto.TestSdk.Tests.ExecutionTests

open Expecto
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open YoloDev.Expecto.TestSdk.Tests.Fakes

let private outcomes (handle: FrameworkHandle) =
  Expect.isEmpty handle.Errors "adapter errors"
  handle.Results |> List.map (fun r -> r.name, r.outcome) |> List.sort

let private reportedUids () =
  discover ()
  |> List.map (fun case -> case.FullyQualifiedName, reportedUid case)
  |> Map.ofList

let tests =
  testSequenced
  <| testList
    "execution"
    [
      testCase "running without a filter runs every test"
      <| fun () ->
        let expected =
          [
            "fixture.alpha", TestOutcome.Passed
            "fixture.beta", TestOutcome.Passed
            "fixture.gamma", TestOutcome.Passed
            "fixture.skipped", TestOutcome.Skipped
          ]

        Expect.equal (outcomes (run None)) expected "outcomes"

      testCase "running one reported uid runs exactly that test"
      <| fun () ->
        let filter = uidListFilter [ (reportedUids ())["fixture.gamma"] ]
        Expect.equal (outcomes (run (Some filter))) [ "fixture.gamma", TestOutcome.Passed ] "outcomes"

      testCase "running several reported uids runs exactly those tests"
      <| fun () ->
        let uids = reportedUids ()
        let filter = uidListFilter [ uids["fixture.alpha"]; uids["fixture.skipped"] ]

        Expect.equal
          (outcomes (run (Some filter)))
          [ "fixture.alpha", TestOutcome.Passed; "fixture.skipped", TestOutcome.Skipped ]
          "outcomes"

      testCase "running every reported uid runs every test"
      <| fun () ->
        let filter = uidListFilter (reportedUids () |> Map.values)
        Expect.equal (outcomes (run (Some filter))) (outcomes (run None)) "outcomes"

      testCase "running by FullyQualifiedName still works"
      <| fun () ->
        Expect.equal
          (outcomes (run (Some "FullyQualifiedName=fixture.beta")))
          [ "fixture.beta", TestOutcome.Passed ]
          "outcomes"
    ]
