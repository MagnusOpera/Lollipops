namespace Lollipops;
using System.Text.Json;
using Microsoft.VisualBasic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public record Package {
    public required string Id { get; init; }
    public string? Version { get; init; }
    public bool PreRelease { get; init; }
}

public class Container {
}

public record Configuration {
    public required Package[] Packages { get; init; }
}


public static class ConfigurationExtensions {

    public static async Task Install(this Configuration configuration, string storage) {
        if (!Directory.Exists(storage)) {
            Directory.CreateDirectory(storage);
        }

        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        var repository = Repository.Factory.GetCoreV3(source);
        var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
        var sourceCacheContext = new SourceCacheContext();


        foreach (var package in configuration.Packages) {
            var metadata = await searchPackage(package);
            if (metadata is null) {
                Console.WriteLine($"Failed to resolve {package}");
            } else {
                Console.WriteLine($"Successfully resolved {package} with version {metadata.Identity.Version}");
            }
        }


        async Task<IPackageSearchMetadata?> searchPackage(Package package) {
            if (package.Version is not null) {
                if (!NuGetVersion.TryParseStrict(package.Version, out var nugetVersion)) {
                    throw new ApplicationException($"Invalid version '{package.Version}' for package '{package.Id}'");
                }

                var packageIdentity = new PackageIdentity(package.Id, nugetVersion);
                var packageMetadata = await packageMetadataResource.GetMetadataAsync(packageIdentity,
                                                                                     sourceCacheContext,
                                                                                     NullLogger.Instance,
                                                                                     CancellationToken.None);
                return packageMetadata;
            } else {
                var results = await packageMetadataResource.GetMetadataAsync(package.Id,
                                                                             package.PreRelease,
                                                                             false,
                                                                             sourceCacheContext,
                                                                             NullLogger.Instance,
                                                                             CancellationToken.None);
                return results.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            }
        }
    }
}

