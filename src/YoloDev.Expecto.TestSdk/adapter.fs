namespace YoloDev.Expecto.TestSdk

open Expecto.Tests
open System.Threading
open System.Diagnostics
open Microsoft.Testing.Extensions.VSTestBridge
open Microsoft.Testing.Extensions.VSTestBridge.Requests
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open System.Threading.Tasks
open System.Reflection

[<FileExtension(".dll")>]
[<FileExtension(".exe")>]
[<DefaultExecutorUri(Constants.executorUriString)>]
[<ExtensionUri(Constants.executorUriString)>]
type VsTestAdapter() =
  let cts = new CancellationTokenSource()

  member x.WaitForDebugger() =
    if not Debugger.IsAttached then
      Debugger.Launch() |> ignore

    while (not cts.IsCancellationRequested && not Debugger.IsAttached) do
      printfn "Waiting for debugger to attach to process: %d" (Process.GetCurrentProcess().Id)
      Thread.Sleep 1000

    Debugger.Break()

  member x.Breakpoint() =
    if
      System.Environment.GetEnvironmentVariable("DEBUG_EXPECTO_TESTSDK", System.EnvironmentVariableTarget.Process) = "1"
    then
      x.WaitForDebugger()

  interface System.IDisposable with
    member x.Dispose() =
      cts.Dispose()

  interface ITestDiscoverer with
    member x.DiscoverTests(sources, discoveryContext, logger, discoverySink) =
      x.Breakpoint()

      let stopwatch = Stopwatch.StartNew()
      let logger = Logger(logger, stopwatch)

      let runSettings =
        Option.ofObj discoveryContext.RunSettings
        |> Option.map (RunSettings.read logger)
        |> Option.defaultValue RunSettings.defaultSettings

      Discovery.discoverTestCases logger runSettings sources
      |> Seq.map ExpectoTestCase.case
      |> Seq.iter discoverySink.SendTestCase

  interface ITestExecutor with
    member x.Cancel() = cts.Cancel()

    member x.RunTests(tests: TestCase seq, runContext: IRunContext | null, frameworkHandle: IFrameworkHandle | null) : unit =
      x.Breakpoint()
      let tests = Guard.argNotNull "tests" tests
      let runContext = Guard.argNotNull "runContext" runContext
      let frameworkHandle = Guard.argNotNull "frameworkHandle" frameworkHandle

      let stopwatch = Stopwatch.StartNew()
      let logger = Logger(frameworkHandle, stopwatch)

      let runSettings =
        Option.ofObj runContext
        |> Option.bind (fun c -> Option.ofObj c.RunSettings)
        |> Option.map (RunSettings.read logger)
        |> Option.defaultValue RunSettings.defaultSettings

      Execution.runSpecifiedTests logger runSettings frameworkHandle tests
      |> Async.RunSynchronously

    member x.RunTests(sources: string seq, runContext: IRunContext | null, frameworkHandle: IFrameworkHandle | null) : unit =
      x.Breakpoint()
      let sources = Guard.argNotNull "sources" sources
      let runContext = Guard.argNotNull "runContext" runContext
      let frameworkHandle = Guard.argNotNull "frameworkHandle" frameworkHandle

      let stopwatch = Stopwatch.StartNew()
      let logger = Logger(frameworkHandle, stopwatch)

      let runSettings =
        Option.ofObj runContext
        |> Option.bind (fun c -> Option.ofObj c.RunSettings)
        |> Option.map (RunSettings.read logger)
        |> Option.map (RunSettings.filter runContext)
        |> Option.defaultValue RunSettings.defaultSettings

      let testPlatformContext =
        { requireSourceInformation = runSettings.collectSourceInformation
          requireTestProperty = true }

      Execution.runTests logger runSettings frameworkHandle sources
      |> Async.RunSynchronously

/// Defines the identity of the Expecto extension for Microsoft.Testing.Platform.
type ExpectoExtension() =
    interface Microsoft.Testing.Platform.Extensions.IExtension with
      member _.Uid = nameof(ExpectoExtension)
      member _.Version =
        match Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>() with
        | null -> "0.0.0"
        | x -> x.InformationalVersion
      member _.DisplayName = "Expecto"
      member _.Description = "Expecto test adapter for Microsoft Testing Platform"
      member _.IsEnabledAsync() = System.Threading.Tasks.Task.FromResult true

/// Defines the ITestFramework extension of Microsoft.Testing.Platform for Expecto using the VSTest bridge.
type ExpectoTestFramework(extension, getTestAssemblies, serviceProvider, capabilities) =
  inherit SynchronizedSingleSessionVSTestBridgedTestFramework(extension, getTestAssemblies, serviceProvider, capabilities) with

  let vstestAdapter = new VsTestAdapter()

  let discoverTests (request: VSTestDiscoverTestExecutionRequest) =
    let discoverer = vstestAdapter :> ITestDiscoverer
    discoverer.DiscoverTests(request.AssemblyPaths, request.DiscoveryContext, request.MessageLogger, request.DiscoverySink)
    Task.CompletedTask

  let runTests (request: VSTestRunTestExecutionRequest) (token: CancellationToken) =
    let runner = vstestAdapter :> ITestExecutor
    use _ = token.Register (fun _ -> runner.Cancel())
    match request.VSTestFilter.TestCases |> Option.ofNullable with
    | Some testCases -> runner.RunTests(testCases, request.RunContext, request.FrameworkHandle)
    | None -> runner.RunTests(request.AssemblyPaths, request.RunContext, request.FrameworkHandle)
    Task.CompletedTask

  override _.SynchronizedDiscoverTestsAsync(request, _, _) = discoverTests request
  override _.SynchronizedRunTestsAsync(request, _, token) = runTests request token
