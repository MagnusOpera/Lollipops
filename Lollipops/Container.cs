namespace MagnusOpera.Lollipops;
using System.ComponentModel.Composition.Hosting;

public interface IContainer {
    T Resolve<T>(string? name = null);
}

internal class Container(CompositionContainer container) : IContainer {
    public T Resolve<T>(string? name = null) {
        return container.GetExport<T>(name).Value;
    }
}
