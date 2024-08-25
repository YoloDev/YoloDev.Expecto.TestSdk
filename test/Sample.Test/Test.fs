module Test

open Expecto

[<Tests>]
let tests =
  testList "list1" [
    testCase "test1" (fun () -> ())
    testCase "test2" (fun () -> ())
    // testCase "test2" (fun () -> failwithf "I died :(")
    // testCase "test3" (fun () -> failwithf "And thus, I have becometh death!")
    testCase "test4" (fun () -> skiptestf "Yes. Much death") ]
