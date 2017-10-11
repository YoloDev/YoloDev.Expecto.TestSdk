namespace YoloDev.Expecto.TestSdk

open System.IO
open System.Threading
open System.Diagnostics
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging

module Constants = 
  [<Literal>]
  let executorUri = "executor://yolodev/expecto"

open Constants

module internal Guard =
  let inline argNotNull (name: string) (arg: 'a) =
    match arg with
    | null -> nullArg name
    | _ -> arg

type RunSettings = {
  /// Gets a value which indicates whether we should attempt to get source line information.
  collectSourceInformation: bool
   
  /// Gets a value which indicates whether we're running in design mode inside the IDE.
  designMode: bool
  
  /// Gets a value which indicates if we should disable parallelization.
  disableParallelization: bool
  
  /// Gets a value which indicates the target framework the tests are being run in.
  targetFrameworkVersion: string option }

[<RequireQualifiedAccess>]
module internal Seq =
  let bind f s = seq { 
    for i in s do 
    yield! f i }

[<RequireQualifiedAccess>]
module internal Xml =
  open System.Xml.Linq

  let read s = 
    try XDocument.Parse s |> Option.ofObj
    with _ -> None

  let root (doc: XDocument) = doc.Root |> Option.ofObj

  let element name (e: XElement) = e.Element (XName.Get name) |> Option.ofObj

  let value (e: XElement) = e.Value |> Option.ofObj

[<RequireQualifiedAccess>]
module internal TryParse =
  open System

  let bool (str: string) =
    match Boolean.TryParse str with
    | true, v -> Some v
    | _       -> None

module RunSettings =
  let defaultSettings = {
    collectSourceInformation = true
    designMode = true
    disableParallelization = false
    targetFrameworkVersion = None }
  
  let read (runSettings: IRunSettings) =
    let settings = defaultSettings
    let confNode =
      Option.ofObj runSettings
      |> Option.bind (fun s -> Option.ofObj s.SettingsXml)
      |> Option.bind Xml.read
      |> Option.bind Xml.root
      |> Option.bind (Xml.element "RunSettings")
      |> Option.bind (Xml.element "RunConfiguration")
    
    let settings =
      confNode
      |> Option.bind (Xml.element "DisableParallelization")
      |> Option.bind Xml.value
      |> Option.bind TryParse.bool
      |> Option.map (fun v -> { settings with disableParallelization = v })
      |> Option.defaultValue settings
    
    let settings =
      confNode
      |> Option.bind (Xml.element "DesignMode")
      |> Option.bind Xml.value
      |> Option.bind TryParse.bool
      |> Option.map (fun v -> { settings with designMode = v })
      |> Option.defaultValue settings
    
    let settings =
      confNode
      |> Option.bind (Xml.element "CollectSourceInformation")
      |> Option.bind Xml.value
      |> Option.bind TryParse.bool
      |> Option.map (fun v -> { settings with collectSourceInformation = v })
      |> Option.defaultValue settings
    
    let settings =
      confNode
      |> Option.bind (Xml.element "TargetFrameworkVersion")
      |> Option.bind Xml.value
      |> Option.map (fun v -> { settings with targetFrameworkVersion = Some v })
      |> Option.defaultValue settings
    
    settings

type TestPlatformContext = {
  /// Indicates if VSTestCase object must have FileName or LineNumber information.
  requireSourceInformation: bool

  /// Indicates if TestCase needs to be serialized in VSTestCase instance.
  requireTestProperty: bool }

type LogLevel = Info | Warning | Error

type Logger (logger: IMessageLogger, stopwatch: Stopwatch) =
  member private x.send (level: LogLevel) (assemblyName: string) (message: string) =
    let level = 
      match level with 
      | Info -> TestMessageLevel.Informational 
      | Warning -> TestMessageLevel.Warning 
      | Error -> TestMessageLevel.Error
    
    let assemblyText =
      match assemblyName with
      | null -> ""
      | s    -> sprintf "%s: " <| Path.GetFileNameWithoutExtension s
    
    logger.SendMessage (level, sprintf "[Expecto %s] %s%s" (string stopwatch.Elapsed) assemblyText message)

module Discovery =
  let createTestCase (source: string) (test: Expecto.FlatTest) =
    TestCase (test.name, System.Uri executorUri, source)

  let discoverTests (sources: string seq) = seq {
    for source in sources do
      let assembly = System.Reflection.Assembly.LoadFile source
      
      if isNull assembly then
        failwithf "LoadFind %s returned null" source

      let test = Expecto.Impl.testFromAssembly assembly
      match test with
      | None -> yield! Seq.empty
      | Some test ->
        let tests = Expecto.Test.toTestCodeList test
        for test in tests do
          yield (test, source)
    }

[<FileExtension(".dll")>]
[<FileExtension(".exe")>]
[<DefaultExecutorUri(executorUri)>]
[<ExtensionUri(executorUri)>]
type VsTestAdapter () =
  let cts = new CancellationTokenSource ()

  interface System.IDisposable with
    member x.Dispose () =
      match cts with
      | null -> ()
      | s    -> s.Dispose ()

  interface ITestDiscoverer with
    member x.DiscoverTests (sources, discoveryContext, logger, discoverySink) =
      let last = ref "start"
      let step m = last := m

      try
        let sources = Guard.argNotNull "sources" sources
        let logger = Guard.argNotNull "logger" logger
        let discoverySink = Guard.argNotNull "discoverySink" discoverySink

        step "start stopwatch"
        let stopwatch = Stopwatch.StartNew ()

        step "create logger"
        let logger = Logger (logger, stopwatch)

        step "run settings"
        let runSettings =
          Option.ofObj discoveryContext
          |> Option.bind (fun c -> Option.ofObj c.RunSettings)
          |> Option.map RunSettings.read
          |> Option.defaultValue RunSettings.defaultSettings

        step "context"
        let testPlatformContext = {
          requireSourceInformation = runSettings.collectSourceInformation
          requireTestProperty = true }
        
        step "discover"
        Discovery.discoverTests sources
        |> Seq.iter (fun (test, source) ->
          let testCase = Discovery.createTestCase source test
          discoverySink.SendTestCase testCase)
      
      with e ->
        let msg = sprintf "Step: %s\nExn: %s" !last (e.ToString ())
        raise <| System.Exception (msg, e)
  
  interface ITestExecutor with
    member x.Cancel () = failwith "Not implemented (cancel)"
    member x.RunTests (tests: TestCase seq, runContext: IRunContext, frameworkHandle: IFrameworkHandle) : unit = failwith "Not implemented (run testcases)"
    member x.RunTests (sources: string seq, runContext: IRunContext, frameworkHandle: IFrameworkHandle) : unit = failwith "Not implemented (run sources)"
