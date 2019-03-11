namespace YoloDev.Expecto.TestSdk

open System.IO
open System.Threading
open System.Diagnostics
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging

[<FileExtension(".dll")>]
[<FileExtension(".exe")>]
[<DefaultExecutorUri(Constants.executorUriString)>]
[<ExtensionUri(Constants.executorUriString)>]
type VsTestAdapter () =
  let cts = new CancellationTokenSource ()
  
  interface System.IDisposable with
    member x.Dispose () =
      match cts with
      | null -> ()
      | s    -> s.Dispose ()

  interface ITestDiscoverer with
    member x.DiscoverTests (sources, discoveryContext, logger, discoverySink) =
      let sources = Guard.argNotNull "sources" sources
      let logger = Guard.argNotNull "logger" logger
      let discoverySink = Guard.argNotNull "discoverySink" discoverySink

      let stopwatch = Stopwatch.StartNew ()
      let logger = Logger (logger, stopwatch)

      let runSettings =
        Option.ofObj discoveryContext
        |> Option.bind (fun c -> Option.ofObj c.RunSettings)
        |> Option.map (RunSettings.read logger)
        |> Option.defaultValue RunSettings.defaultSettings

      let testPlatformContext = {
        requireSourceInformation = runSettings.collectSourceInformation
        requireTestProperty = true }
      
      Discovery.discoverTestCases logger sources
      |> Seq.map ExpectoTestCase.case
      |> Seq.iter discoverySink.SendTestCase
  
  interface ITestExecutor with
    member x.Cancel () = cts.Cancel ()

    member x.RunTests (tests: TestCase seq, runContext: IRunContext, frameworkHandle: IFrameworkHandle) : unit = 
      let tests = Guard.argNotNull "tests" tests
      let runContext = Guard.argNotNull "runContext" runContext
      let frameworkHandle = Guard.argNotNull "frameworkHandle" frameworkHandle

      let stopwatch = Stopwatch.StartNew ()
      let logger = Logger (frameworkHandle, stopwatch)

      let runSettings =
        Option.ofObj runContext
        |> Option.bind (fun c -> Option.ofObj c.RunSettings)
        |> Option.map (RunSettings.read logger)
        |> Option.defaultValue RunSettings.defaultSettings
      
      Execution.runSpecifiedTests logger runSettings.expectoConfig frameworkHandle tests
      |> Async.RunSynchronously

    member x.RunTests (sources: string seq, runContext: IRunContext, frameworkHandle: IFrameworkHandle) : unit =
      let sources = Guard.argNotNull "sources" sources
      let runContext = Guard.argNotNull "runContext" runContext
      let frameworkHandle = Guard.argNotNull "frameworkHandle" frameworkHandle

      let stopwatch = Stopwatch.StartNew ()
      let logger = Logger (frameworkHandle, stopwatch)

      let runSettings =
        Option.ofObj runContext
        |> Option.bind (fun c -> Option.ofObj c.RunSettings)
        |> Option.map (RunSettings.read logger)
        |> Option.defaultValue RunSettings.defaultSettings

      let testPlatformContext = {
        requireSourceInformation = runSettings.collectSourceInformation
        requireTestProperty = true }
      
      Execution.runTests logger runSettings.expectoConfig frameworkHandle sources
      |> Async.RunSynchronously
