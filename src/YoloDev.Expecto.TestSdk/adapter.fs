namespace YoloDev.Expecto.TestSdk

open Expecto.Tests
open System.Threading
open System.Diagnostics
open Microsoft.Testing.Extensions.VSTestBridge
open Microsoft.Testing.Extensions.VSTestBridge.Requests
open Microsoft.Testing.Platform.Extensions.Messages
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
        runContext.RunSettings |> Option.ofObj
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
        runContext.RunSettings
        |> Option.ofObj
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
    runner.RunTests(request.AssemblyPaths, request.RunContext, request.FrameworkHandle)
    Task.CompletedTask

  override _.SynchronizedDiscoverTestsAsync(request, _, _) = discoverTests request
  override _.SynchronizedRunTestsAsync(request, _, token) = runTests request token

  // Expecto tests are values in nested test lists, not CLR methods, so there is no real managed
  // type/method. We synthesize a public TestMethodIdentifierProperty from the (dot-joined)
  // FullyQualifiedName so Visual Studio can recover namespace/class/method via location.type /
  // location.method without the legacy vstest.TestCase.* key/value-pair properties.
  // NOTE: this assumes the default `--join-with .` separator; per-row identity relies on TestNode.Uid.
  override _.AddAdditionalProperties(testNode: TestNode, testCase: TestCase) =
    let fqn = testCase.FullyQualifiedName
    if not (System.String.IsNullOrEmpty fqn) then
      let lastDot = fqn.LastIndexOf '.'
      let typeName, methodName =
        if lastDot > 0 then fqn.Substring(0, lastDot), fqn.Substring(lastDot + 1)
        else "", fqn
      let assemblyFullName =
        try AssemblyName.GetAssemblyName(testCase.Source).FullName
        with _ -> ""
      let property =
        TestMethodIdentifierProperty(
          assemblyFullName, "", typeName, methodName, 0, Array.empty<string>, "System.Void")
      testNode.Properties.Add property
