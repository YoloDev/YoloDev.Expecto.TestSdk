module YoloDev.Expecto.TestSdk.Tests.Main

open Expecto

// The tests are listed explicitly rather than found with runTestsInAssembly, because Fixture.tests is
// marked [<Tests>] for the adapter under test to discover.
let tests =
  testList
    "YoloDev.Expecto.TestSdk"
    [
      IdTests.tests
      FilterTests.tests
      ExecutionTests.tests
      ServerModeTests.tests
    ]

[<EntryPoint>]
let main args = runTestsWithCLIArgs [] args tests
