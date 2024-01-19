using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using Lollipops;


var config = new Configuration
{
    Packages = [new Package { Id = "MagnusOpera.PresqueYaml", Version = "0.24.0" },
                new Package { Id = "MagnusOpera.PresqueYaml", Version = "0.23.0" }]
};

var currentDir = Environment.CurrentDirectory;
var containerBuilder = await config.Install(Path.Combine(currentDir, "test-lollipops"));

var programAssembly = new AssemblyCatalog(Assembly.GetExecutingAssembly());
containerBuilder.Add(programAssembly);
var container = containerBuilder.Build();

var toto = container.Resolve<IExtension>("toto");
toto.Say("Hello Lollipops");

var titi = container.Resolve<IExtension>("titi");
titi.Say("Hello Lollipops");

public interface IExtension {
    void Say(string msg);
}


[Export("toto", typeof(IExtension))]
public class Toto : IExtension {
    public void Say(string msg) {
        Console.WriteLine($"{msg} from Toto");
    }
}

[Export("titi", typeof(IExtension))]
public class Titi : IExtension {
    public void Say(string msg) {
        Console.WriteLine($"{msg} from Titi");
    }
}
