/// A minimal Microsoft.Testing.Platform server-mode client: the JSON-RPC protocol IDEs (Visual Studio, Rider)
/// use to discover and run tests.
module YoloDev.Expecto.TestSdk.Tests.ServerModeClient

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open System.Text
open System.Text.Json.Nodes

type TestNode =
  {
    uid: string
    name: string
    state: string
  }

let private required what (value: 'T | null) =
  match value with
  | null -> failwithf "server-mode message is missing %s" what
  | value -> value

let private stringAt (name: string) (json: JsonNode) =
  (required name json[name]).GetValue<string>()

let private jsonObject (properties: (string * JsonNode) list) =
  let json = JsonObject()

  for name, value in properties do
    json[name] <- value

  json

let private jsonString (value: string) =
  match JsonValue.Create value with
  | null -> failwithf "could not encode %s as JSON" value
  | json -> json :> JsonNode

type Client(app: string) =
  let listener = new TcpListener(IPAddress.Loopback, 0)
  do listener.Start()
  let port = (listener.LocalEndpoint :?> IPEndPoint).Port

  let serverProcess =
    let startInfo =
      ProcessStartInfo("dotnet", $"exec \"{app}\" --server --client-port {port}")

    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    match Process.Start startInfo with
    | null -> failwithf "could not start test app %s" app
    | started -> started

  let socket =
    let accept = listener.AcceptTcpClientAsync()

    if not (accept.Wait(TimeSpan.FromSeconds 30.)) then
      failwithf "test app %s did not connect: %s" app (serverProcess.StandardError.ReadToEnd())

    accept.Result

  let stream = socket.GetStream()
  do stream.ReadTimeout <- 30_000
  let mutable nextId = 0

  let readContentLength () =
    let headers = StringBuilder()

    while not (headers.ToString().EndsWith "\r\n\r\n") do
      match stream.ReadByte() with
      | -1 -> failwith "test app closed the connection"
      | b -> headers.Append(char b) |> ignore

    headers.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
    |> Array.pick (fun line ->
      match line.Split(':', 2) with
      | [| name; value |] when name.Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ->
        Some(int (value.Trim()))
      | _ -> None)

  let receive () =
    let body = Array.zeroCreate<byte>(readContentLength ())
    stream.ReadExactly(body, 0, body.Length)
    (required "a JSON body" (JsonNode.Parse body)).AsObject()

  let send (message: JsonObject) =
    message["jsonrpc"] <- jsonString "2.0"
    let body = Encoding.UTF8.GetBytes(message.ToJsonString())
    let header = Encoding.ASCII.GetBytes $"Content-Length: {body.Length}\r\n\r\n"
    stream.Write(header, 0, header.Length)
    stream.Write(body, 0, body.Length)

  let testNode (change: JsonNode | null) =
    let change = required "change" change
    let node = required "node" change["node"]

    {
      uid = stringAt "uid" node
      name = stringAt "display-name" node
      state = stringAt "execution-state" node
    }

  /// Sends a request and returns every test node update received before its response.
  let request (method: string) (parameters: JsonObject) =
    nextId <- nextId + 1
    let id = nextId

    send (jsonObject [ "id", JsonValue.Create id; "method", jsonString method; "params", parameters ])

    let rec loop updates =
      let message = receive ()

      match message["id"] with
      | null ->
        match message["method"] with
        | null -> loop updates
        | notification when notification.GetValue<string>() = "testing/testUpdates/tests" ->
          let changes =
            match (required "params" message["params"])["changes"] with
            | null -> []
            | changes -> [ for change in changes.AsArray() -> testNode change ]

          loop (updates @ changes)
        | _ -> loop updates
      | responseId when responseId.GetValue<int>() = id ->
        match message["error"] with
        | null -> updates
        | error -> failwithf "%s failed: %s" method (error.ToJsonString())
      | _ -> loop updates

    loop []

  let runId () = jsonString (Guid.NewGuid().ToString())

  do
    request
      "initialize"
      (jsonObject
        [
          "processId", JsonValue.Create Environment.ProcessId
          "clientInfo",
          jsonObject
            [
              "name", jsonString "YoloDev.Expecto.TestSdk.Tests"
              "version", jsonString "1.0"
            ]
          "capabilities", jsonObject [ "testing", jsonObject [ "debuggerProvider", JsonValue.Create false ] ]
        ])
    |> ignore

  member _.Discover() =
    request "testing/discoverTests" (jsonObject [ "runId", runId () ])

  /// Runs tests the way an IDE does: by sending discovered test nodes back, identified by uid.
  /// Returns the final state of each test that reported one.
  member _.Run(nodes: TestNode seq) =
    let tests = JsonArray()

    for node in nodes do
      tests.Add(
        jsonObject
          [
            "uid", jsonString node.uid
            "display-name", jsonString node.name
            "node-type", jsonString "action"
          ]
      )

    request "testing/runTests" (jsonObject [ "runId", runId (); "tests", tests ])
    |> List.filter (fun node -> node.state <> "in-progress")
    |> List.map (fun node -> node.name, node.state)
    |> List.sort

  interface IDisposable with
    member _.Dispose() =
      try
        send (jsonObject [ "method", jsonString "exit"; "params", JsonObject() ])
      with _ ->
        ()

      if not (serverProcess.WaitForExit 10_000) then
        serverProcess.Kill true

      socket.Dispose()
      listener.Stop()
