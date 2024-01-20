using System.Reflection;
using MagnusOpera.Lollipops;

var appDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var slnDir = Path.GetFullPath(Path.Combine(appDir, "../../../../.."));

var nugetLocal = Path.GetFullPath(Path.Combine(slnDir, ".nugets"));
var projectDir = Path.GetFullPath(Path.Combine(slnDir, ".lollipops"));

var config = new Configuration
{
    Source = nugetLocal,
    Packages = [new Package { Id = "TestCSharp" },
                new Package { Id = "TestFSharp" }]
};

var containerBuilder = await config.Install(projectDir);

var container = containerBuilder.Build();

var csharp = container.Resolve<TestCommon.ILogger>("TestCSharp");
csharp.Log("Hello Lollipops");

var fsharp = container.Resolve<TestCommon.ILogger>("TestFSharp");
fsharp.Log("Hello Lollipops");
