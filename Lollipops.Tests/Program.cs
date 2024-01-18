using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Lollipops;


var config = new Configuration
{
    Packages = [new Package { Id = "MagnusOpera.PresqueYaml", Version = "0.24.0" }]
};

var currentDir = Environment.CurrentDirectory;
var containerBuilder = await config.Install(Path.Combine(currentDir, "test-lollipops"));
var programAssembly = new AssemblyCatalog(Assembly.GetExecutingAssembly());
containerBuilder.Add(programAssembly);

var container = containerBuilder.Build();
var toto1 = container.Resolve<IToto>("toto");
toto1.Say("Hello from Lollipops");

var toto2 = container.Resolve<IToto>("toto");
toto2.Say("Hello from Lollipops 2");


public interface IToto {
    void Say(string msg);
}


[Export("toto", typeof(IToto))]
public class Toto : IToto {
    public void Say(string msg) {
        Console.WriteLine($"{msg} from instance {GetHashCode()}");
    }
}
