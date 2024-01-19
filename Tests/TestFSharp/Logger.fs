namespace TestFSharp

type Logger() =
    interface TestCommon.ILogger with
        member _.Log msg =
            printfn $"TestFSharp: {msg}"
