[<AutoOpen>]
module YoloDev.Expecto.TestSdk.Logging

open System.IO
open System.Diagnostics
open Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging

[<RequireQualifiedAccess>]
type LogLevel =
  | Info
  | Warning
  | Error

type Logger(logger: IMessageLogger, stopwatch: Stopwatch) =
  member internal x.Send (level: LogLevel) (assemblyName: string) (message: string) =
    let level =
      match level with
      | LogLevel.Info -> TestMessageLevel.Informational
      | LogLevel.Warning -> TestMessageLevel.Warning
      | LogLevel.Error -> TestMessageLevel.Error

    let assemblyText =
      match assemblyName with
      | null -> ""
      | s -> sprintf "%s: " <| Path.GetFileNameWithoutExtension s

    logger.SendMessage(level, sprintf "[Expecto %s] %s%s" (string stopwatch.Elapsed) assemblyText message)

[<RequireQualifiedAccess>]
module Logger =
  let send level assembly message (logger: Logger) =
    let assembly = Option.defaultValue null assembly
    logger.Send level assembly message
