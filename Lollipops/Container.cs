namespace Lollipops;
using System.ComponentModel.Composition.Hosting;

public interface IContainer {
    T Resolve<T>(string? name = null);
}

public class Container(CompositionContainer container) : IContainer {
    public T Resolve<T>(string? name = null) {
        return container.GetExport<T>(name).Value;
    }
}
