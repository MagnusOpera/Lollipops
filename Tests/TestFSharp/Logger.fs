namespace TestFSharp
open System.ComponentModel.Composition

[<Export("TestFSharp", typeof<TestCommon.ILogger>)>]
type Logger() =
    interface TestCommon.ILogger with
        member _.Log msg =
            printfn $"{msg} from F# !"
