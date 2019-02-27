[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Settings

open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Expecto.Impl
open Expecto.Tests
open System
open Expecto

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
    expectoConfig : ExpectoConfig}

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
    | Stress_Timeout n -> fun o -> { o with stressTimeout = TimeSpan.FromMinutes n }
    | Stress_Memory_Limit n -> fun o -> { o with stressMemoryLimit = n }
    | Fail_On_Focused_Tests -> fun o -> { o with failOnFocusedTests = true }
    | Debug -> fun o -> { o with verbosity = Expecto.Logging.LogLevel.Debug }
    | Log_Name name -> fun o -> { o with logName = Some name }
    | Filter hiera -> fun o -> {o with filter = Test.filter (fun s -> s.StartsWith hiera )}
    | Run tests -> fun o -> {o with filter = Test.filter (fun s -> tests |> List.exists ((=) s) )}
    | FsCheck_Max_Tests n -> fun o -> {o with fsCheckMaxTests = n }
    | FsCheck_Start_Size n -> fun o -> {o with fsCheckStartSize = n }
    | FsCheck_End_Size n -> fun o -> {o with fsCheckEndSize = Some n }
    | Allow_Duplicate_Names -> fun o -> { o with allowDuplicateNames = true }
    | No_Spinner -> fun o -> { o with noSpinner = true }
    | _ -> id

  let readValueParse parser elementName confNode =
      confNode
      |> Option.bind (Xml.element elementName)
      |> Option.bind Xml.value
      |> Option.bind parser

  let readValueString elementName confNode = readValueParse Option.ofObj elementName confNode
  let readValueBool elementName confNode = readValueParse TryParse.bool elementName confNode
  let readValueInt32 elementName confNode = readValueParse TryParse.int32 elementName confNode
  let readValueFloat32 elementName confNode = readValueParse TryParse.float32 elementName confNode

  let readExpectoConfig expectoConfig (confNode: Xml.Linq.XElement option) =
    let parallelTests = 
      readValueBool "parallel" 
      >> Option.map ( function | true -> CLIArguments.Parallel | false -> CLIArguments.Sequenced)
    let parallelWorkers = 
      readValueInt32 "parallel-workers"
      >> Option.map CLIArguments.Parallel_Workers
    let stress = 
      readValueFloat32 "stress"
      >> Option.map CLIArguments.Stress
    let stressTimeout = 
      readValueFloat32 "stress-timeout"
      >> Option.map CLIArguments.Stress_Timeout
    let stressMemoryLimit = 
      readValueFloat32 "stress-memory-limit"
      >> Option.map CLIArguments.Stress_Memory_Limit
    let failOnFocusedTests =
      readValueBool "fail-on-focused-tests"
      >> Option.bind ( function | true -> Some CLIArguments.Fail_On_Focused_Tests | false -> None)
    let debug =
      readValueBool "debug"
      >> Option.bind ( function | true -> Some CLIArguments.Debug | false -> None)
    let logName =
      readValueString "log-name"
      >> Option.map CLIArguments.Log_Name
    let filter =
      readValueString "filter"
      >> Option.map CLIArguments.Filter
    //TODO: Run_tests.  Need to figure out how to parse it
    let fsCheckMaxTests =
      readValueInt32 "fscheck-max-tests"
      >> Option.map CLIArguments.FsCheck_Max_Tests
    let fsCheckStartSize =
      readValueInt32 "fscheck-start-size"
      >> Option.map CLIArguments.FsCheck_Start_Size
    let fsCheckEndSize =
      readValueInt32 "fscheck-end-size"
      >> Option.map CLIArguments.FsCheck_End_Size
    let allowDuplicateNames =
      readValueBool "allow-duplicate-name"
      >> Option.bind (function | true -> Some CLIArguments.Allow_Duplicate_Names | false -> None)
    let spinner =
      readValueBool "no-spinner"
      >> Option.bind (function | true -> Some CLIArguments.No_Spinner | false -> None)

    let args =
      [
        parallelTests
        parallelWorkers
        stress
        stressTimeout
        stressMemoryLimit
        failOnFocusedTests
        debug
        logName
        filter
        fsCheckMaxTests
        fsCheckStartSize
        fsCheckEndSize
        allowDuplicateNames
        spinner
      ]
      |> Seq.choose (fun readSetting -> readSetting confNode)

    (expectoConfig, args)
    ||> Seq.fold(fun state next -> foldCLIArgumentToConfig next state)
    

  let read (runSettings: IRunSettings) =
    let settings = defaultSettings
    let runSettingsNode =
      Option.ofObj runSettings
      |> Option.bind (fun s -> Option.ofObj s.SettingsXml)
      |> Option.bind Xml.read
      |> Option.bind Xml.root //This gets the RunSettings element
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

    let settings = 
      { settings 
        with expectoConfig = readExpectoConfig settings.expectoConfig expectoRunSettings }
    settings

type TestPlatformContext = 
  { /// Indicates if VSTestCase object must have FileName or LineNumber information.
    requireSourceInformation: bool

    /// Indicates if TestCase needs to be serialized in VSTestCase instance.
    requireTestProperty: bool }