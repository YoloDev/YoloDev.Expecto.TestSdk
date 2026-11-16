module YoloDev.Expecto.TestSdk.Tests.ServerModeTests

open System
open Expecto
open YoloDev.Expecto.TestSdk.Tests.ServerModeClient

/// Path to test/Sample.Test built with EnableExpectoTestingPlatformIntegration=true; set by `just test-adapter`.
let private sampleApp = Environment.GetEnvironmentVariable "EXPECTO_SAMPLE_TEST_APP"

let private withClient f () =
  if String.IsNullOrEmpty sampleApp then
    skiptest "EXPECTO_SAMPLE_TEST_APP is not set; run `just test-adapter`"

  use client = new Client(sampleApp)
  f client

let private nodesByName (client: Client) =
  client.Discover() |> List.map (fun n -> n.name, n) |> Map.ofList

let tests =
  testSequenced
  <| testList
    "server mode (IDE protocol)"
    [
      testCase "discovery reports every sample test"
      <| withClient (fun client ->
        let names = client.Discover() |> List.map (fun n -> n.name) |> List.sort
        Expect.equal names [ "list1.test1"; "list1.test2"; "list1.test4" ] "discovered")

      testCase "running one discovered uid runs exactly that test"
      <| withClient (fun client ->
        let nodes = nodesByName client
        Expect.equal (client.Run [ nodes["list1.test2"] ]) [ "list1.test2", "passed" ] "results")

      testCase "running several discovered uids runs exactly those tests"
      <| withClient (fun client ->
        let nodes = nodesByName client

        Expect.equal
          (client.Run [ nodes["list1.test1"]; nodes["list1.test4"] ])
          [ "list1.test1", "passed"; "list1.test4", "skipped" ]
          "results")

      testCase "running every discovered uid runs every test"
      <| withClient (fun client ->
        Expect.equal
          (client.Run(client.Discover()))
          [ "list1.test1", "passed"; "list1.test2", "passed"; "list1.test4", "skipped" ]
          "results")

      testCase "uids are stable across discoveries in one session"
      <| withClient (fun client ->
        let uids () =
          client.Discover() |> List.map (fun n -> n.name, n.uid) |> Map.ofList

        Expect.equal (uids ()) (uids ()) "uids")

      testCase "a uid that was never discovered runs nothing"
      <| withClient (fun client ->
        let unknown =
          {
            uid = System.Guid.NewGuid().ToString()
            name = "list1.test1"
            state = "discovered"
          }

        Expect.isEmpty (client.Run [ unknown ]) "results")
    ]
