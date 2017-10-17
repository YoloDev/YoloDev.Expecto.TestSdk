[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Settings

open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter

type RunSettings = 
  { /// Gets a value which indicates whether we should attempt to get source line information.
    collectSourceInformation: bool

    /// Gets a value which indicates whether we're running in design mode inside the IDE.
    designMode: bool

    /// Gets a value which indicates if we should disable parallelization.
    disableParallelization: bool

    /// Gets a value which indicates the target framework the tests are being run in.
    targetFrameworkVersion: string option }

[<RequireQualifiedAccess>]
module RunSettings = 
  let defaultSettings = 
    { collectSourceInformation = true
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

type TestPlatformContext = 
  { /// Indicates if VSTestCase object must have FileName or LineNumber information.
    requireSourceInformation: bool

    /// Indicates if TestCase needs to be serialized in VSTestCase instance.
    requireTestProperty: bool }