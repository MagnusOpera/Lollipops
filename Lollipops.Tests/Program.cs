using Lollipops;


var config = new Configuration
{
    Packages = [new Package { Id = "MagnusOpera.PresqueYaml", Version = "0.24.0" }]
};

await config.Install("toto");

