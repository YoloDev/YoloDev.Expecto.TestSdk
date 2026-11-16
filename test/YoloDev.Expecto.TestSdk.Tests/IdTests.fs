module YoloDev.Expecto.TestSdk.Tests.IdTests

open Expecto
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open YoloDev.Expecto.TestSdk
open YoloDev.Expecto.TestSdk.Tests.Fakes

let tests =
  testList
    "test case ids"
    [
      testCase "discovery finds every fixture test"
      <| fun () ->
        let names =
          discover () |> List.map (fun case -> case.FullyQualifiedName) |> List.sort

        Expect.equal names (List.sort Fixture.names) "discovered names"

      // Reading Id caches it, so this must not touch Id before the swap: the bridge doesn't either.
      testCase "ids survive the bridge replacing ExecutorUri"
      <| fun () ->
        for case in discover () do
          let expected =
            TestCase(case.FullyQualifiedName, Constants.executorUri, case.Source).Id.ToString()

          Expect.equal (reportedUid case) expected $"id of {case.FullyQualifiedName}"

      testCase "ids are the ones VSTest derives from the Expecto executor"
      <| fun () ->
        for case in discover () do
          let expected =
            TestCase(case.FullyQualifiedName, Constants.executorUri, case.Source).Id

          Expect.equal case.Id expected $"id of {case.FullyQualifiedName}"

      testCase "ids are stable across discoveries"
      <| fun () ->
        let ids () =
          discover ()
          |> List.map (fun case -> case.FullyQualifiedName, case.Id)
          |> Map.ofList

        Expect.equal (ids ()) (ids ()) "ids from two discoveries"

      testCase "ids are unique"
      <| fun () ->
        let ids = discover () |> List.map (fun case -> case.Id)
        Expect.equal (List.distinct ids |> List.length) ids.Length "distinct ids"
    ]
