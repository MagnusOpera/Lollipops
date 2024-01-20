namespace TestCSharp;
using System.ComponentModel.Composition;

[Export("TestCSharp", typeof(TestCommon.ILogger))]
public class Logger : TestCommon.ILogger {
    public void Log(string msg) {
        Console.WriteLine($"{msg} from C# !");
    }
}
