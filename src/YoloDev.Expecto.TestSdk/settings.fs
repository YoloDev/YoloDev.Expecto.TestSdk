[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Settings

open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Expecto.Impl
open Expecto.Tests
open System
open Expecto
open System.Xml.Linq
open Expecto.Logging

type RunSettings = 
  { /// Gets a value which indicates whether we should attempt to get source line information.
    collectSourceInformation: bool

    /// Gets a value which indicates whether we're running in design mode inside the IDE.
    designMode: bool

    /// Gets a value which indicates if we should disable parallelization.
    disableParallelization: bool

    /// Gets a value which indicates the target framework the tests are being run in.
    targetFrameworkVersion: string option 

    /// Gets the [ExpectoConfig](https://github.com/haf/expecto#the-config) that was set via RunSettings
    expectoConfig : ExpectoConfig }

[<RequireQualifiedAccess>]
module RunSettings = 
  let defaultSettings = 
    { collectSourceInformation = true
      designMode = true
      disableParallelization = false
      targetFrameworkVersion = None
      expectoConfig = ExpectoConfig.defaultConfig }

  let private foldCLIArgumentToConfig = function
    | Sequenced -> fun o -> { o with ``parallel`` = false }
    | Parallel -> fun o -> { o with ``parallel`` = true }
    | Parallel_Workers n -> fun o -> { o with parallelWorkers = n }
    | Stress s  -> fun o -> { o with stress = Some (TimeSpan.FromMinutes s) }
    | Stress_Timeout n -> fun o -> { o with stressTimeout = TimeSpan.FromMinutes n }
    | Stress_Memory_Limit n -> fun o -> { o with stressMemoryLimit = n }
    | Fail_On_Focused_Tests -> fun o -> { o with failOnFocusedTests = true }
    | CLIArguments.Debug -> fun o -> { o with verbosity = Expecto.Logging.LogLevel.Debug }
    | Log_Name name -> fun o -> { o with logName = Some name }
    | Filter hiera -> fun o -> {o with filter = Test.filter (fun s -> s.StartsWith hiera )}
    | Run tests -> fun o -> {o with filter = Test.filter (fun s -> tests |> List.exists ((=) s) )}
    | FsCheck_Max_Tests n -> fun o -> {o with fsCheckMaxTests = n }
    | FsCheck_Start_Size n -> fun o -> {o with fsCheckStartSize = n }
    | FsCheck_End_Size n -> fun o -> {o with fsCheckEndSize = Some n }
    | Allow_Duplicate_Names -> fun o -> { o with allowDuplicateNames = true }
    | No_Spinner -> fun o -> { o with noSpinner = true }
    | Filter_Test_List _ -> id
    | Filter_Test_Case _  -> id
    // Not applicable
    | List_Tests -> id
    // Not applicable
    | Summary -> id
    // Not applicable
    | Summary_Location -> id
    // Not applicable
    | Version -> id
    // Not applicable
    | My_Spirit_Is_Weak -> id
    // Not applicable, printer gets overriden in execution.fs
    | Printer _ -> id
    | Verbosity l -> fun o -> { o with verbosity = l }
    // Not applicable 
    | Append_Summary_Handler(_) -> id

  let readValueParse parser elementName confNode =
      confNode
      |> Option.bind (Xml.element elementName)
      |> Option.bind Xml.value
      |> Option.bind parser

  let readValueString elementName confNode = readValueParse Option.ofObj elementName confNode
  let readValueBool elementName confNode = readValueParse TryParse.bool elementName confNode

  let (|Element|_|) (name : string) (node : XElement) =
    if name.Equals (node.Name.LocalName, StringComparison.OrdinalIgnoreCase)
    then Some node.Value
    else None

  let (|Bool|_|) str = TryParse.bool str
  let (|Int|_|) str = TryParse.int32 str
  let (|Float|_|) str = TryParse.float str
  let (|String|_|) (str : string) = Option.ofObj str

  let readExpectoConfig (logger : YoloDev.Expecto.TestSdk.Logging.Logger) expectoConfig (confNode: Xml.Linq.XElement) =
    let configMapper node = 
      match node with
      | Element "sequenced" (Bool true) -> Some CLIArguments.Sequenced
      | Element "sequenced" (Bool false) -> Some CLIArguments.Parallel
      | Element "parallel" (Bool true) -> Some CLIArguments.Parallel
      | Element "parallel" (Bool false) -> Some CLIArguments.Sequenced
      | Element "parallel-workers" (Int i) -> Some (CLIArguments.Parallel_Workers i)
      | Element "stress" (Float f) -> Some (CLIArguments.Stress f)
      | Element "stress-timeout" (Float f) -> Some (CLIArguments.Stress_Timeout f)
      | Element "stress-memory-limit" (Float f) -> Some (CLIArguments.Stress_Memory_Limit f)
      | Element "stress-memory-limit" (Float f) -> Some (CLIArguments.Stress_Memory_Limit f)
      | Element "fail-on-focused-tests" (Bool true) -> Some CLIArguments.Fail_On_Focused_Tests
      | Element "debug" (Bool true) -> Some CLIArguments.Debug
      | Element "log-name" (String s) -> Some (CLIArguments.Log_Name s)
      | Element "filter" (String s) -> Some (CLIArguments.Filter s)
      | Element "fscheck-max-tests" (Int i) -> Some (CLIArguments.FsCheck_Max_Tests i)
      | Element "fscheck-start-size" (Int i) -> Some (CLIArguments.FsCheck_Max_Tests i)
      | Element "fscheck-end-size" (Int i) -> Some (CLIArguments.FsCheck_End_Size i)
      | Element "allow-duplicate-name" (Bool true) -> Some CLIArguments.Allow_Duplicate_Names
      | Element "no-spinner" (Bool true) -> Some CLIArguments.No_Spinner
      | Element "verbosity" (String s) -> Expecto.Logging.LogLevel.ofString s |> CLIArguments.Verbosity |> Some
      | unknown ->  
          sprintf "Unknown config key for Expecto : %s=%s" unknown.Name.LocalName unknown.Value |> logger.Send LogLevel.Warning "" 
          None

    let args =
      confNode.Descendants()
      |> Seq.choose configMapper

    (expectoConfig, args)
    ||> Seq.fold(fun state next -> foldCLIArgumentToConfig next state)
    

  let read logger (runSettings: IRunSettings) =
    let settings = defaultSettings

    let runSettingsNode =
      Option.ofObj runSettings
      |> Option.bind (fun s -> Option.ofObj s.SettingsXml)
      |> Option.bind Xml.read
      |> Option.bind Xml.root

    let confNode = 
      runSettingsNode
      |> Option.bind (Xml.element "RunConfiguration")
    
    let settings = 
      confNode
      |> readValueBool "DisableParallelization"
      |> Option.map (fun v -> { settings with disableParallelization = v })
      |> Option.defaultValue settings
    
    let settings = 
      confNode
      |> readValueBool "DesignMode"
      |> Option.map (fun v -> { settings with designMode = v })
      |> Option.defaultValue settings
    
    let settings = 
      confNode
      |> readValueBool "CollectSourceInformation"
      |> Option.map (fun v -> { settings with collectSourceInformation = v })
      |> Option.defaultValue settings
    
    let settings = 
      confNode
      |> readValueString "TargetFrameworkVersion"
      |> Option.map (fun v -> { settings with targetFrameworkVersion = Some v })
      |> Option.defaultValue settings

    let expectoRunSettings = 
      runSettingsNode
      |> Option.bind (Xml.element "Expecto")

    let expectoConfig =
      expectoRunSettings
      |> Option.map(readExpectoConfig logger settings.expectoConfig)
      |> Option.defaultValue settings.expectoConfig

    { settings with expectoConfig =  expectoConfig }

type TestPlatformContext = 
  { /// Indicates if VSTestCase object must have FileName or LineNumber information.
    requireSourceInformation: bool

    /// Indicates if TestCase needs to be serialized in VSTestCase instance.
    requireTestProperty: bool }