[<AutoOpen>]
module YoloDev.Expecto.TestSdk.Execution

open Expecto
open Expecto.Impl
open System.Reflection
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging


module private LogAdapter =
  let create (logger: Logger) (assembly: string option) name =
    let map =
      function
      | Expecto.Logging.LogLevel.Verbose
      | Expecto.Logging.LogLevel.Debug -> None
      | Expecto.Logging.LogLevel.Info  -> Some LogLevel.Info
      | Expecto.Logging.LogLevel.Warn  -> Some LogLevel.Warning
      | Expecto.Logging.LogLevel.Error
      | Expecto.Logging.LogLevel.Fatal -> Some LogLevel.Error

    let withLogger l fn =
      match map l with
      | None -> async.Zero ()
      | Some level ->
        async {
          use writer = new System.IO.StringWriter ()
          let writerLogger =
            Expecto.Logging.TextWriterTarget (Array.empty, Expecto.Logging.LogLevel.Info, writer)
            :> Expecto.Logging.Logger

          do! fn writerLogger l
          Logger.send level assembly (string writer) logger
        }

    let log l fn =
      withLogger l <| fun logger l -> logger.log l fn

    let logWithAck l fn =
      withLogger l <| fun logger l -> logger.logWithAck l fn

    { new Expecto.Logging.Logger with
      member x.name = name
      member x.logWithAck level fn = logWithAck level fn
      member x.log level fn = log level fn }

module private PrinterAdapter =
  open System

  let create (cases: Map<string, TestCase>) (frameworkHandle: IFrameworkHandle) =
    let results = Map.map (fun _ -> TestResult) cases

    let inline recordEnd (result: TestResult) =
      frameworkHandle.RecordEnd (result.TestCase, result.Outcome)
      frameworkHandle.RecordResult result
      async.Zero ()

    // let beforeRun _ = async.Zero ()

    let beforeEach name =
      let result = Map.find name results
      result.StartTime <- DateTimeOffset.Now
      frameworkHandle.RecordStart result.TestCase
      async.Zero ()

    // let info _ = async.Zero ()

    let passed name duration =
      let result = Map.find name results
      result.Outcome <- TestOutcome.Passed
      result.EndTime <- DateTimeOffset.Now
      result.Duration <- duration
      recordEnd result

    let ignored name reason =
      let result = Map.find name results
      result.Outcome <- TestOutcome.Skipped
      result.EndTime <- DateTimeOffset.Now
      result.Messages.Add <| TestResultMessage (TestResultMessage.AdditionalInfoCategory, sprintf "Skipped: %s" reason)
      recordEnd result

    let failed name reason duration =
      let result = Map.find name results
      result.Outcome <- TestOutcome.Failed
      result.ErrorMessage <- reason
      result.EndTime <- DateTimeOffset.Now
      result.Duration <- duration
      recordEnd result

    let exn name (exn: exn) duration =
      let result = Map.find name results
      result.Outcome <- TestOutcome.Failed
      result.ErrorMessage <- exn.Message
      result.ErrorStackTrace <- exn.StackTrace
      result.EndTime <- DateTimeOffset.Now
      result.Duration <- duration
      recordEnd result

    { Expecto.Impl.TestPrinters.silent with
        beforeEach = beforeEach
        passed = passed
        ignored = ignored
        failed = failed
        exn = exn }

[<RequireQualifiedAccess>]
module Execution =
    /// Prints out names of all tests for given test suite.
  let private duplicatedNames test =
    test
    |> Test.toTestCodeList
    |> Seq.toList
    |> List.groupBy (fun t -> t.name)
    |> List.choose ( function
      | _, x :: _ :: _ ->
        Some x.name
      | _ ->
        None
    )

  let private runMatchingTests logger config (test: ExpectoTest) (cases: TestCase list) (frameworkHandle: IFrameworkHandle) =
    // TODO: Context passing
    // TODO: fail on focused tests
    let discovered =
      Discovery.getTestCasesFromTest logger test
      |> Seq.map (fun t -> String.concat "/" (ExpectoTestCase.name t), ExpectoTestCase.case t)
      |> Map.ofSeq

    let getLogger = LogAdapter.create logger (Some <| ExpectoTest.source test)
    let printAdapter = PrinterAdapter.create discovered frameworkHandle

    let config = CLIArguments.Printer printAdapter :: config
    Expecto.Logging.Global.initialise <| { Expecto.Logging.Global.defaultConfig with getLogger = getLogger }

    let testNames =
      cases
      |> Seq.map (fun c -> c.DisplayName)
      |> Set.ofSeq

    let tests =
      ExpectoTest.test test
      |> Expecto.Test.filter "/" (fun name -> Seq.contains (String.concat "/" name) testNames)

    let duplicates = duplicatedNames tests
    match duplicates with
    | [] ->
      Expecto.Tests.runTestsWithCLIArgs config [||] tests |> ignore
      async.Zero ()
    | _  ->
      Logger.send LogLevel.Error (Some <| ExpectoTest.source test) (sprintf "Found duplicated test names, these names are: %A" duplicates) logger
      async.Zero ()

  let private runTestsForSource logger config frameworkHandle source =
    match Discovery.discoverTestForSource logger source with
    | None -> async.Zero ()
    | Some test ->
      let cases =
        Discovery.getTestCasesFromTest logger test
        |> Seq.map ExpectoTestCase.case
        |> List.ofSeq
      runMatchingTests logger config test cases frameworkHandle

  let private runSpecifiedTestsForSource logger config frameworkHandle source (tests: TestCase seq) =
    let nameSet = tests |> Seq.map (fun t -> t.FullyQualifiedName) |> Set.ofSeq
    match Discovery.discoverTestForSource logger source with
    | None -> async.Zero ()
    | Some test ->
      let cases =
        Discovery.getTestCasesFromTest logger test
        |> Seq.map ExpectoTestCase.case
        |> Seq.filter (fun c -> Set.contains c.FullyQualifiedName nameSet)
        |> List.ofSeq

      runMatchingTests logger config test cases frameworkHandle

  let runTests logger config frameworkHandle sources =
    async {
      for source in sources do
        do! runTestsForSource logger config frameworkHandle source
    }

  let runSpecifiedTests logger config frameworkHandle (tests: TestCase seq) =
    let bySource = tests |> Seq.groupBy (fun t -> t.Source)

    async {
      for source, tests in bySource do
        do! runSpecifiedTestsForSource logger config frameworkHandle source tests
    }

