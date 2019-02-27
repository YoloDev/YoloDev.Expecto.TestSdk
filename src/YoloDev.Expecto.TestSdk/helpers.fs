[<AutoOpen>]
module internal YoloDev.Expecto.TestSdk.Helpers

[<RequireQualifiedAccess>]
module internal Guard = 
  let inline argNotNull (name: string) (arg: 'a) = 
    match arg with
    | null -> nullArg name
    | _ -> arg

[<RequireQualifiedAccess>]
module internal Seq = 
  let bind f s = 
    seq { 
      for i in s do
        yield! f i
    }

[<RequireQualifiedAccess>]
module internal Xml = 
  open System.Xml.Linq
  
  let read s = 
    try 
      XDocument.Parse s |> Option.ofObj
    with _ -> None
  
  let root (doc: XDocument) = doc.Root |> Option.ofObj
  let element name (e: XElement) = e.Element (XName.Get name) |> Option.ofObj
  let value (e: XElement) = e.Value |> Option.ofObj

[<RequireQualifiedAccess>]
module internal TryParse = 
  open System
  
  let bool (str: string) = 
    match Boolean.TryParse str with
    | true, v -> Some v
    | _ -> None

  let int32 (str : string) =
    match Int32.TryParse str with
    | true,v -> Some v
    | _ -> None

  let float32 (str : string) =
    match Double.TryParse str with
    | true,v -> Some v
    | _ -> None