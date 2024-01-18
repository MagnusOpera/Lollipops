using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Lollipops;


var config = new Configuration
{
    Packages = [new Package { Id = "MagnusOpera.PresqueYaml", Version = "0.24.0" }]
};

var containerBuilder = await config.Install("/Users/pierre/src/MagnusOpera/Lollipops/toto");
var programAssembly = new AssemblyCatalog(Assembly.GetExecutingAssembly());
containerBuilder.Add(programAssembly);

var container = containerBuilder.Build();
var toto = container.Resolve<IToto>("toto");
toto.Say("Hello from Lollipops");



public interface IToto {
    void Say(string msg);
}


[Export("toto", typeof(IToto))]
public class Toto : IToto {
    public void Say(string msg) {
        Console.WriteLine(msg);
    }
}
