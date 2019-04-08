[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Settings

open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Expecto.Impl
open Expecto.Tests
open System
open Expecto
open System.Xml.Linq
open Expecto.Logging

[<RequireQualifiedAccess>]
module internal SettingsParser =

  type ParserState = YoloDev.Expecto.TestSdk.Logging.Logger * Map<string, CLIArguments option>
  type SettingParser = ParserState * XElement -> CLIArguments option

  type SettingsParser = 
    { state: ParserState
      parsers: Map<string, SettingParser> }
  
  let build logger parsers =
    { state = logger, Map.empty
      parsers = Map.ofList parsers }
  
  let bool (_: ParserState, x: XElement) =
    TryParse.bool x.Value
  
  let int (_: ParserState, x: XElement) =
    TryParse.int32 x.Value
  
  let float (_: ParserState, x: XElement) =
    TryParse.float x.Value
  
  let exclusive (name: string) ((logger, s): ParserState, x: XElement) =
    match Map.tryFind name s with
    | None -> ()
    | Some _ ->
      sprintf "Setting %s provided with %s, but they are mutually exclusive." name x.Name.LocalName |> logger.Send LogLevel.Warning ""
      ()
  
  let parse (parser: SettingsParser) (x: XElement) =
    let name = x.Name.LocalName.ToLowerInvariant ()
    match Map.tryFind name parser.parsers with
    | None -> parser
    | Some p ->
      let arg = p (parser.state, x)
      let (logger, args) = parser.state
      let args = Map.add name arg args
      { parser with state = (logger, args) }
  
  let result (parser: SettingsParser) =
    let (_, args) = parser.state
    args


let private (>?>) (f: 'a -> 'b option) (g: 'b -> 'c option) =
  fun (a: 'a) ->
    match f a with
    | None -> None
    | Some b -> g b

let private (>->) f g = f >?> (g >> Some)

let private ( *>) (f: 'a -> unit) (g: 'a -> 'b) =
  fun (a: 'a) ->
    f a
    g a

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
    expectoConfig : CLIArguments list }

[<RequireQualifiedAccess>]
module RunSettings = 
  let defaultSettings = 
    { collectSourceInformation = true
      designMode = true
      disableParallelization = false
      targetFrameworkVersion = None
      expectoConfig = [] }

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
    let parser =
      SettingsParser.build logger [
        "sequenced", SettingsParser.exclusive "parallel" *> SettingsParser.bool >?> function | true -> Some CLIArguments.Sequenced | false -> Some CLIArguments.Parallel
        "parallel", SettingsParser.exclusive "parallel" *> SettingsParser.bool >?> function | true -> Some CLIArguments.Parallel | false -> Some CLIArguments.Sequenced
        "parallel-workers", SettingsParser.int >-> CLIArguments.Parallel_Workers
        "stress", SettingsParser.float >-> CLIArguments.Stress
        "stress-timeout", SettingsParser.float >-> CLIArguments.Stress_Timeout
        "stress-memory-limit", SettingsParser.float >-> CLIArguments.Stress_Memory_Limit
        "fail-on-focused-tests", SettingsParser.bool >?> function | true -> Some CLIArguments.Fail_On_Focused_Tests | false -> None
        "debug", SettingsParser.bool >?> function | true -> Some CLIArguments.Debug | false -> None
      ]

    let args =
      confNode.Descendants()
      |> Seq.fold SettingsParser.parse parser
      |> SettingsParser.result
    
    let args =
      match Map.tryFind "fail-on-focused-tests" args with
      | Some _ -> args
      | None   -> Map.add "fail-on-focused-tests" (Some CLIArguments.Fail_On_Focused_Tests) args
    
    args
    |> Map.toSeq
    |> Seq.choose snd
    |> List.ofSeq
    

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