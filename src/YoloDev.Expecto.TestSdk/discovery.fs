[<AutoOpen>]
module YoloDev.Expecto.TestSdk.Discovery

open Expecto
open Expecto.Impl
open System.Reflection
open Microsoft.VisualStudio.TestPlatform.ObjectModel
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging

module private TestCase =
  let create (test: FlatTest) (assembly: Assembly) (source: string) =
    let case = TestCase (String.concat "/" test.name, Constants.executorUri, source)
    let location = getLocation assembly test.test
    case.LineNumber <- location.lineNumber
    case.CodeFilePath <- location.sourcePath
    case

type ExpectoTest =
  private { source: string
            assembly: Assembly
            test: Test }

[<RequireQualifiedAccess>]
module ExpectoTest =
  let create source assembly test =
    { source = source
      assembly = assembly
      test = test }

  let source test = test.source

  let test test = test.test

type ExpectoTestCase =
  private { test: ExpectoTest
            case: FlatTest
            mstest: Lazy<TestCase> }

[<RequireQualifiedAccess>]
module ExpectoTestCase =
  let create test case =
    { test = test
      case = case
      mstest = lazy (TestCase.create case test.assembly test.source ) }

  let name c = c.case.name

  let case c = c.mstest.Force ()

[<RequireQualifiedAccess>]
module Discovery =
  let private getTestForAssembly logger assembly source =
    let assembly = Guard.argNotNull "assembly" assembly
    let source = Guard.argNotNull "source" source

    // Logger.send Info (Some source) "Finding tests in assembly" logger
    Expecto.Impl.testFromAssembly assembly
    |> Option.map (ExpectoTest.create source assembly)

  let internal getTestCasesFromTest logger (test: ExpectoTest) =
    Expecto.Test.toTestCodeList test.test
    |> List.map (ExpectoTestCase.create test)

  let internal discoverTestForSource logger source =
    let assembly = System.Reflection.Assembly.LoadFile source

    if isNull assembly then
      failwithf "LoadFile %s returned null" source

    getTestForAssembly logger assembly source

  let discoverTests logger =
    Seq.choose (discoverTestForSource logger)

  let discoverTestCases logger =
    discoverTests logger >> Seq.collect (getTestCasesFromTest logger)
