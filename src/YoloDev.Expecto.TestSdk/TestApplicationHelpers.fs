namespace YoloDev.Expecto.TestSdk

open System.Reflection

open Microsoft.Testing.Extensions.VSTestBridge.Capabilities
open Microsoft.Testing.Extensions.VSTestBridge.Helpers
open Microsoft.Testing.Platform.Builder
open Microsoft.Testing.Platform.Capabilities.TestFramework

module TestApplicationBuilderExtensions =
  let addExpectoFramework (getTestAssemblies: unit -> Assembly seq) (builder: ITestApplicationBuilder) =
    let expectoExtension = ExpectoExtension()
    builder.AddRunSettingsService expectoExtension
    builder.AddTestCaseFilterService expectoExtension
    builder.RegisterTestFramework (
      (fun _ -> TestFrameworkCapabilities(VSTestBridgeExtensionBaseCapabilities())),
      (fun capabilities serviceProvider -> new ExpectoTestFramework(expectoExtension, getTestAssemblies, serviceProvider, capabilities))
    ) |> ignore

module TestingPlatformBuilderHook =
  let AddExtensions(builder: ITestApplicationBuilder, arguments: string array) =
    TestApplicationBuilderExtensions.addExpectoFramework (fun () -> [ Assembly.GetEntryAssembly() ]) builder
