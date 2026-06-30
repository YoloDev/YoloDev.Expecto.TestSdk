namespace YoloDev.Expecto.TestSdk

open System.Reflection

open Microsoft.Testing.Extensions.TrxReport.Abstractions
open Microsoft.Testing.Extensions.VSTestBridge.Helpers
open Microsoft.Testing.Platform.Builder
open Microsoft.Testing.Platform.Capabilities.TestFramework

// We intentionally do NOT use VSTestBridgeExtensionBaseCapabilities because that type also implements
// INamedFeatureCapability and advertises the "vstestProvider" capability. When a server advertises
// vstestProvider, Visual Studio Test Explorer consumes the legacy "vstest.TestCase.*" properties
// (serialized through Microsoft.Testing.Platform's internal SerializableKeyValuePairStringProperty
// key/value bag) instead of the public, structured location.* properties.
//
// ExpectoTestFramework.AddAdditionalProperties now emits the public TestMethodIdentifierProperty
// (serialized as location.type / location.method), so by not advertising vstestProvider we let the IDE
// consume the public properties and stop depending on the internal key/value-pair shape.
//
// This mirrors MSTest's MSTestCapabilities:
// https://github.com/microsoft/testfx/blob/5c6ea3bf01f1247736fbbbba0ffdd8a8b38840dc/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/TestApplicationBuilderExtensions.cs
// MSTest can implement the internal IInternalVSTestBridgeTrxReportCapability (it is on the bridge's
// InternalsVisibleTo list); external adapters cannot, so we implement the public ITrxReportCapability.
type internal ExpectoTestFrameworkCapabilities() =
  interface ITrxReportCapability with
    member _.IsSupported = true
    member _.Enable() = ()

module TestApplicationBuilderExtensions =
  let addExpectoFramework (getTestAssemblies: unit -> Assembly seq) (builder: ITestApplicationBuilder) =
    let expectoExtension = ExpectoExtension()
    builder.AddRunSettingsService expectoExtension
    builder.AddTestCaseFilterService expectoExtension
    builder.RegisterTestFramework (
      (fun _ -> TestFrameworkCapabilities(ExpectoTestFrameworkCapabilities())),
      (fun capabilities serviceProvider -> new ExpectoTestFramework(expectoExtension, getTestAssemblies, serviceProvider, capabilities))
    ) |> ignore

module TestingPlatformBuilderHook =
  let AddExtensions(builder: ITestApplicationBuilder, arguments: string array) =
    TestApplicationBuilderExtensions.addExpectoFramework (fun () -> [ Assembly.GetEntryAssembly() ]) builder
