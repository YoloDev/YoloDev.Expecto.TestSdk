module Test

open Expecto

[<Tests>]
let tests =
  testList "list1" [
    testCase "test1" (fun () -> ())
    testCase "test2" (fun () -> ())
    #if FAILING_TESTS
    testCase "test-failing-1" (fun () -> failwithf "I died :(")
    testCase "test-failing-2" (fun () -> failwithf "And thus, I have becometh death!")
    #endif
    testCase "test4" (fun () -> skiptestf "Yes. Much death") ]
