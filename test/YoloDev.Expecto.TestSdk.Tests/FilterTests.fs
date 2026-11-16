module YoloDev.Expecto.TestSdk.Tests.FilterTests

open Expecto
open YoloDev.Expecto.TestSdk
open YoloDev.Expecto.TestSdk.Tests.Fakes

let private selectedBy filter =
  let testFilter = TestFilter.create (runContext (Some filter))

  discover ()
  |> List.filter (TestFilter.matches testFilter)
  |> List.map (fun case -> case.FullyQualifiedName)
  |> List.sort

let private idOf name =
  (Map.find name (discoverByName ())).Id.ToString()

let tests =
  testList
    "test case filter"
    [
      testCase "Id selects exactly the matching test"
      <| fun () -> Expect.equal (selectedBy ("Id=" + idOf "fixture.beta")) [ "fixture.beta" ] "selected"

      testCase "a uid list selects exactly those tests"
      <| fun () ->
        let filter = uidListFilter [ idOf "fixture.alpha"; idOf "fixture.gamma" ]
        Expect.equal (selectedBy filter) [ "fixture.alpha"; "fixture.gamma" ] "selected"

      testCase "uids reported through the bridge select the test cases built for execution"
      <| fun () ->
        let reported =
          discover ()
          |> List.map (fun case -> case.FullyQualifiedName, reportedUid case)
          |> Map.ofList

        let filter = uidListFilter [ reported["fixture.beta"]; reported["fixture.skipped"] ]
        Expect.equal (selectedBy filter) [ "fixture.beta"; "fixture.skipped" ] "selected"

      testCase "Id matching ignores case"
      <| fun () ->
        let filter = "Id=" + (idOf "fixture.alpha").ToUpperInvariant()
        Expect.equal (selectedBy filter) [ "fixture.alpha" ] "selected"

      testCase "an unknown Id selects nothing"
      <| fun () -> Expect.isEmpty (selectedBy $"Id={System.Guid.NewGuid()}") "selected"

      testCase "Id can be excluded"
      <| fun () ->
        let expected = Fixture.names |> List.filter ((<>) "fixture.gamma") |> List.sort
        Expect.equal (selectedBy ("Id!=" + idOf "fixture.gamma")) expected "selected"

      testCase "FullyQualifiedName still selects"
      <| fun () -> Expect.equal (selectedBy "FullyQualifiedName=fixture.alpha") [ "fixture.alpha" ] "selected"

      testCase "Name (the display name) still selects"
      <| fun () -> Expect.equal (selectedBy "Name~gamma") [ "fixture.gamma" ] "selected"

      testCase "Id and FullyQualifiedName combine"
      <| fun () ->
        let filter = "Id=" + idOf "fixture.alpha" + "|FullyQualifiedName=fixture.beta"
        Expect.equal (selectedBy filter) [ "fixture.alpha"; "fixture.beta" ] "selected"
    ]
