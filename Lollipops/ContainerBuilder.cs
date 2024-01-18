namespace Lollipops;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition;

public interface IContainerBuilder {
    void Add(ComposablePartCatalog part);
    IContainer Build();
}

internal class ContainerBuilder(AggregateCatalog aggregateCatalog) : IContainerBuilder {
    public void Add(ComposablePartCatalog part) {
        aggregateCatalog.Catalogs.Add(part);
    }

    public IContainer Build() {
        var container = new CompositionContainer(aggregateCatalog);
        container.ComposeParts();

        return new Container(container);
    }
}
