module YoloDev.Expecto.TestSdk.Logging

type LogLevel = Info | Warning | Error
type Logger = LogLevel -> (unit -> string) -> unit

type ILoggerProvider =
  abstract Logger: Logger

[<RequireQualifiedAccess>]
module Logger =
  let inline provide (p: ILoggerProvider) = p.Logger

  let inline create (log: LogLevel -> string -> unit) (minLevel: LogLevel) : Logger =
    fun (level: LogLevel) (fn: unit -> string) ->
      match level, minLevel with
      | Info, Info
      | Warning, Info
      | Warning, Warning
      | Error, Info
      | Error, Warning
      | Error, Error     -> log level (fn ())
      | _                -> ()

type Log<'t> = Log of (Logger -> 't)

[<RequireQualifiedAccess>]
module Log =
  let inline unit x = Log <| fun _ -> x

  let inline bind f (Log ma) = Log <| fun l -> 
    let (Log mb) = f (ma l)
    mb l

  let inline map f ma = bind (f >> unit) ma

  let inline delay f = Log <| fun l ->
    let (Log ma) = f ()
    ma l
  
  let inline combine (Log ma) (Log mb) = Log <| fun l ->
    ma l
    mb l
  
  let inline run l (Log ma) = ma l

module Builder =
  type LogBuilder () =
    member x.Bind (ma, f) = Log.bind f ma
    member x.Delay f = Log.delay f
    member x.Return v = Log.unit v
    member x.ReturnFrom v = v
    member x.Combine (ma, mb) = Log.combine ma mb

let log = Builder.LogBuilder ()