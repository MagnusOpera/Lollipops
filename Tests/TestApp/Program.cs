using System.Reflection;
using MagnusOpera.Lollipops;
using System.ComponentModel.Composition;

// find solution root dir
var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var slnDir = Path.GetFullPath(Path.Combine(appDir, "../../../../.."));

// paths for nuget local folder & install folder
var nugetLocal = Path.GetFullPath(Path.Combine(slnDir, ".nugets"));
var projectDir = Path.GetFullPath(Path.Combine(slnDir, ".lollipops"));

// build container
var config = new Configuration
{
    Source = nugetLocal,
    Packages = [new Package { Id = "TestCSharp" },
                new Package { Id = "TestFSharp" }]
};
var containerBuilder = await config.Install(projectDir);
containerBuilder.Add(Assembly.GetExecutingAssembly());
var container = containerBuilder.Build();

// invoke plugins
var csharp = container.Resolve<TestCommon.ILogger>("TestCSharp");
csharp.Log("Hello Lollipops");

var fsharp = container.Resolve<TestCommon.ILogger>("TestFSharp");
fsharp.Log("Hello Lollipops");

var local = container.Resolve<TestCommon.ILogger>("Local");
local.Log("Hello Lollipops");





[Export("Local", typeof(TestCommon.ILogger))]
public class Logger : TestCommon.ILogger {
    public void Log(string msg) {
        Console.WriteLine($"{msg} from Local !");
    }
}
