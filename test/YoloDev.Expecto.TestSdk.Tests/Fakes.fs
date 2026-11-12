module YoloDev.Expecto.TestSdk.Tests.Fakes

open System
open System.Collections.Concurrent
open Microsoft.VisualStudio.TestPlatform.Common.Filtering
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging
open YoloDev.Expecto.TestSdk

/// The ExecutorUri that Microsoft.Testing.Platform's VSTest bridge puts on every discovered TestCase
/// (ObjectModelConverters.FixUpTestCase) before converting it to a TestNode.
let bridgeExecutorUri = Uri "executor://testingplatform-bridge/v2"

/// Mirrors what the bridge does to a discovered TestCase, then returns the uid it reports to the client.
let reportedUid (case: TestCase) =
  case.ExecutorUri <- bridgeExecutorUri
  case.Id.ToString()

/// The filter the bridge builds from a TestNodeUidListFilter when uids are GUIDs
/// (ContextAdapterBase.BuildFilter).
let uidListFilter (uids: string seq) =
  uids |> Seq.map (sprintf "Id=%s") |> String.concat "|" |> sprintf "(%s)"

let filterExpression (filter: string) =
  let wrapper = FilterExpressionWrapper filter

  match wrapper.ParseError with
  | null -> TestCaseFilterExpression wrapper :> ITestCaseFilterExpression
  | error -> failwithf "invalid filter %s: %s" filter error

let runSettings =
  { new IRunSettings with
      member _.SettingsXml = "<RunSettings><RunConfiguration /></RunSettings>"
      member _.GetSettings _ = null
  }

let runContext (filter: string option) =
  { new IRunContext with
      member _.KeepAlive = false
      member _.InIsolation = false
      member _.IsDataCollectionEnabled = false
      member _.IsBeingDebugged = false
      member _.TestRunDirectory = null
      member _.SolutionDirectory = null

      member _.GetTestCaseFilter(_, _) =
        match filter with
        | Some filter -> filterExpression filter
        | None -> null

      member _.RunSettings = runSettings
  }

type RecordedResult = { name: string; outcome: TestOutcome }

/// Thread-safe like the real handle: Expecto reports results from parallel tests concurrently.
type FrameworkHandle() =
  let results = ConcurrentQueue<RecordedResult>()
  let errors = ConcurrentQueue<string>()

  member _.Results = List.ofSeq results
  member _.Errors = List.ofSeq errors

  interface IFrameworkHandle with
    member val EnableShutdownAfterTestRun = false with get, set
    member _.LaunchProcessWithDebuggerAttached(_, _, _, _) = 0

    member _.RecordResult result =
      results.Enqueue
        {
          name = result.TestCase.FullyQualifiedName
          outcome = result.Outcome
        }

    member _.RecordStart _ = ()
    member _.RecordEnd(_, _) = ()
    member _.RecordAttachments _ = ()

    member _.SendMessage(level, message) =
      if level = TestMessageLevel.Error then
        errors.Enqueue message

let discover () =
  Discovery.discoverTestCases () RunSettings.defaultSettings [ Fixture.source ]
  |> Seq.map ExpectoTestCase.case
  |> List.ofSeq

let discoverByName () =
  discover ()
  |> List.map (fun case -> case.FullyQualifiedName, case)
  |> Map.ofList

/// Runs the fixture through the adapter's VSTest executor, which is what the bridge calls for every run.
let run (filter: string option) =
  let handle = FrameworkHandle()
  use adapter = new VsTestAdapter()

  try
    (adapter :> ITestExecutor).RunTests([ Fixture.source ], runContext filter, handle)
  finally
    // The adapter points Expecto's global logging at the framework handle; without this, the rest of this
    // project's own test run would log into the fake instead of the console.
    Expecto.Logging.Global.initialise Expecto.Logging.Global.defaultConfig

  handle
