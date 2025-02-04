[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Settings

open Microsoft.VisualStudio.TestPlatform.ObjectModel
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

  let bool (_: ParserState, x: XElement) = TryParse.bool x.Value

  let string (_: ParserState, x: XElement) =
    if String.IsNullOrWhiteSpace x.Value then
      None
    else
      Some x.Value

  let int (_: ParserState, x: XElement) = TryParse.int32 x.Value

  let float (_: ParserState, x: XElement) = TryParse.float x.Value

  let exclusive (name: string) ((logger, s): ParserState, x: XElement) =
    match Map.tryFind name s with
    | None -> ()
    | Some _ ->
      sprintf "Setting %s provided with %s, but they are mutually exclusive." name x.Name.LocalName
      |> logger.Send LogLevel.Warning ""

      ()

  let parse (parser: SettingsParser) (x: XElement) =
    let name = x.Name.LocalName.ToLowerInvariant()

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

let private ( *> ) (f: 'a -> unit) (g: 'a -> 'b) =
  fun (a: 'a) ->
    f a
    g a

type TestFilter =
  {
    /// MSTest filter
    expression: ITestCaseFilterExpression option
  }

[<RequireQualifiedAccess>]
module TestFilter =
  let defaultFilter = { TestFilter.expression = None }

  [<RequireQualifiedAccess>]
  module private Properties =
    let fullyQualifiedName = TestCaseProperties.FullyQualifiedName
    let name = TestCaseProperties.DisplayName

    let private all = [ fullyQualifiedName; name ]
    let names = all |> List.map (fun p -> p.Label)
    let dict = all |> Seq.map (fun p -> (p.Label, p)) |> readOnlyDict

    let lookup (name: string) =
      match dict.TryGetValue name with
      | (false, _) -> null
      | (true, prop) -> prop

    let valueProvider (case: TestCase) (propertyName: string) =
      lookup propertyName
      |> Option.ofObj
      |> Option.map (case.GetPropertyValue)
      |> Option.toObj

  let create (context: IRunContext) =
    let expression = context.GetTestCaseFilter(Properties.names, Properties.lookup)
    { TestFilter.expression = expression |> Option.ofObj }

  let matches (filter: TestFilter) (case: TestCase) =
    match filter.expression with
    | None -> true
    | Some expr -> expr.MatchTestCase(case, Properties.valueProvider case)

type RunSettings =
  {
    /// Gets a value which indicates whether we should attempt to get source line information.
    collectSourceInformation: bool

    /// Gets a value which indicates whether we're running in design mode inside the IDE.
    designMode: bool

    /// Gets a value which indicates if we should disable parallelization.
    disableParallelization: bool

    /// Gets a value which indicates the target framework the tests are being run in.
    targetFrameworkVersion: string option

    /// Gets a value which indicates the separator used to join test names in test lists
    joinWith: JoinWith

    /// Gets the [ExpectoConfig](https://github.com/haf/expecto#the-config) that was set via RunSettings
    expectoConfig: CLIArguments list

    /// Test filter
    filter: TestFilter
  }

[<RequireQualifiedAccess>]
module RunSettings =

  let defaultSettings =
    { collectSourceInformation = true
      designMode = true
      disableParallelization = false
      targetFrameworkVersion = None
      joinWith = Dot
      expectoConfig = [ CLIArguments.Colours 0 ]
      filter = TestFilter.defaultFilter }

  let readValueParse parser elementName confNode =
    confNode
    |> Option.bind (Xml.element elementName)
    |> Option.bind Xml.value
    |> Option.bind parser

  let readValueString elementName confNode =
    readValueParse Option.ofObj elementName confNode

  let readValueBool elementName confNode =
    readValueParse TryParse.bool elementName confNode

  let (|Element|_|) (name: string) (node: XElement) =
    if name.Equals(node.Name.LocalName, StringComparison.OrdinalIgnoreCase) then
      Some node.Value
    else
      None

  let (|Bool|_|) str = TryParse.bool str
  let (|Int|_|) str = TryParse.int32 str
  let (|Float|_|) str = TryParse.float str
  let (|String|_|) (str: string) = Option.ofObj str

  let readExpectoConfig (logger: YoloDev.Expecto.TestSdk.Logging.Logger) (confNode: Xml.Linq.XElement) =
    let parser =
      SettingsParser.build
        logger
        [ "sequenced",
          SettingsParser.exclusive "parallel" *> SettingsParser.bool
          >?> function
            | true -> Some CLIArguments.Sequenced
            | false -> Some CLIArguments.Parallel

          "parallel",
          SettingsParser.exclusive "parallel" *> SettingsParser.bool
          >?> function
            | true -> Some CLIArguments.Parallel
            | false -> Some CLIArguments.Sequenced

          "parallel-workers", SettingsParser.int >-> CLIArguments.Parallel_Workers
          "stress", SettingsParser.float >-> CLIArguments.Stress
          "stress-timeout", SettingsParser.float >-> CLIArguments.Stress_Timeout
          "stress-memory-limit", SettingsParser.float >-> CLIArguments.Stress_Memory_Limit
          "fail-on-focused-tests",
          SettingsParser.bool
          >?> function
            | true -> Some CLIArguments.Fail_On_Focused_Tests
            | false -> None

          "debug",
          SettingsParser.bool
          >?> function
            | true -> Some CLIArguments.Debug
            | false -> None

          "colours", SettingsParser.int >-> CLIArguments.Colours

          "join-with", SettingsParser.string >-> CLIArguments.JoinWith ]

    let args =
      confNode.Descendants()
      |> Seq.fold SettingsParser.parse parser
      |> SettingsParser.result

    let args =
      match Map.tryFind "fail-on-focused-tests" args with
      | Some _ -> args
      | None -> Map.add "fail-on-focused-tests" (Some CLIArguments.Fail_On_Focused_Tests) args

    let args =
      match Map.tryFind "join-with" args with
      | Some _ -> args
      | None -> Map.add "join-with" (Some(CLIArguments.JoinWith JoinWith.Dot.asString)) args

    let args =
      match Map.tryFind "colours" args with
      | Some _ -> args
      | None -> Map.add "colours" (Some(CLIArguments.Colours 0)) args

    args |> Map.toSeq |> Seq.choose snd |> List.ofSeq


  let read logger (runSettings: IRunSettings | null) =
    let settings = defaultSettings

    let runSettingsNode =
      Option.ofObj runSettings
      |> Option.bind (fun s -> Option.ofObj s.SettingsXml)
      |> Option.bind Xml.read
      |> Option.bind Xml.root

    let confNode = runSettingsNode |> Option.bind (Xml.element "RunConfiguration")

    let settings =
      confNode
      |> readValueBool "DisableParallelization"
      |> Option.map (fun v ->
        { settings with
            disableParallelization = v })
      |> Option.defaultValue settings

    let settings =
      confNode
      |> readValueBool "DesignMode"
      |> Option.map (fun v -> { settings with designMode = v })
      |> Option.defaultValue settings

    let settings =
      confNode
      |> readValueBool "CollectSourceInformation"
      |> Option.map (fun v ->
        { settings with
            collectSourceInformation = v })
      |> Option.defaultValue settings

    let settings =
      confNode
      |> readValueString "TargetFrameworkVersion"
      |> Option.map (fun v ->
        { settings with
            targetFrameworkVersion = Some v })
      |> Option.defaultValue settings

    let expectoRunSettings = runSettingsNode |> Option.bind (Xml.element "Expecto")

    // Note: this list is *empty* if no expecto run settings are provided
    let expectoConfig =
      expectoRunSettings
      |> Option.map (readExpectoConfig logger)
      |> Option.defaultValue settings.expectoConfig

    let joinWith =
      expectoConfig
      |> List.choose (function
        | CLIArguments.JoinWith "/" -> Some JoinWith.Slash
        | CLIArguments.JoinWith "." -> Some JoinWith.Dot
        | _ -> None)
      |> List.tryHead
      |> Option.defaultValue JoinWith.Dot

    { settings with
        expectoConfig = expectoConfig
        joinWith = joinWith }

  let filter (context: IRunContext) (settings: RunSettings) =
    { settings with
        filter = TestFilter.create context }

type TestPlatformContext =
  {
    /// Indicates if VSTestCase object must have FileName or LineNumber information.
    requireSourceInformation: bool

    /// Indicates if TestCase needs to be serialized in VSTestCase instance.
    requireTestProperty: bool
  }
