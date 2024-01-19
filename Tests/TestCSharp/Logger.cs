namespace TestCSharp;

public class Logger : TestCommon.ILogger {
    public void Log(string msg) {
        Console.WriteLine($"TestCSharp: {msg}");
    }
}
