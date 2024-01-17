namespace Lollipops;

using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.VisualBasic;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.PackageManagement;
using NuGet.Resolver;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System.Xml.Linq;
using System.Globalization;

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



// public class MachineWideSettings : IMachineWideSettings
// {
//     private readonly Lazy<ISettings> _settings;

//     public MachineWideSettings()
//     {
//         _settings = new Lazy<ISettings>(() =>
//         {
//             var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
//             return global::NuGet.Configuration.Settings.LoadMachineWideSettings(baseDirectory);
//         });
//     }

//     public ISettings Settings => _settings.Value;
// }


public static class ConfigurationExtensions {

    public static async Task Install(this Configuration configuration, string downloadFolder) {
        if (!Directory.Exists(downloadFolder)) {
            Directory.CreateDirectory(downloadFolder);
        }

        // var providers = new List<Lazy<INuGetResourceProvider>>();
        // providers.AddRange(Repository.Provider.GetCoreV3()); // Add v3 API s

        // var settings = Settings.LoadDefaultSettings(downloadFolder, null, new MachineWideSettings());
        // var packageSourceProvider = new PackageSourceProvider(settings);
        // var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);

        // var targetFramework = Assembly.GetEntryAssembly()!.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        // var frameworkNameProvider = new FrameworkNameProvider([DefaultFrameworkMappings.Instance], [DefaultPortableFrameworkMappings.Instance]);
        // var nugetFramework = NuGetFramework.ParseFrameworkName(targetFramework!, frameworkNameProvider);

        // var packagePathResolver = new PackagePathResolver(downloadFolder);
        // var project = new FolderNuGetProject(downloadFolder, packagePathResolver, nugetFramework);
        // var packageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, downloadFolder) { PackagesFolderNuGetProject = project };


        // var projectContext = new FolderProjectContext(_logger)
        // {
        //     PackageExtractionContext = new PackageExtractionContext(
        //         PackageSaveMode.Defaultv2,
        //         PackageExtractionBehavior.XmlDocFileSaveMode,
        //         clientPolicyContext,
        //         _logger)
        // };



        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        var repository = Repository.Factory.GetCoreV3(source);
        var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
        var findPackageResource = await repository.GetResourceAsync<FindPackageByIdResource>();
        using var sourceCacheContext = new SourceCacheContext();

        // var rid = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier();

        // Console.WriteLine($"Nuget fx = {nugetFramework}");
        // Console.WriteLine($"rid = {rid}");


        foreach (var package in configuration.Packages) {
            var metadata = await searchPackage(package);
            if (metadata is null) {
                Console.WriteLine($"Failed to resolve {package}");
            } else {
                Console.WriteLine($"Successfully resolved {package} with version {metadata.Identity.Version}");

                // var resolutionContext = new ResolutionContext(
                //     DependencyBehavior.Lowest,
                //     package.PreRelease,
                //     includeUnlisted: true,
                //     VersionConstraints.None);

                // var downloadContext = new PackageDownloadContext(
                //     resolutionContext.SourceCacheContext,
                //     downloadFolder,
                //     resolutionContext.SourceCacheContext.DirectDownload);

                // var projectContext = new FolderProjectContext(NullLogger.Instance);

                // await packageManager.InstallPackageAsync(project, metadata.Identity, resolutionContext, projectContext, downloadContext, repository, [], CancellationToken.None);


                // var compProvider = new CompatibilityProvider(new DefaultFrameworkNameProvider());
                // var reducer = new FrameworkReducer(new DefaultFrameworkNameProvider(), compProvider);



                var downloader = await findPackageResource.GetPackageDownloaderAsync(metadata.Identity, sourceCacheContext, NullLogger.Instance, CancellationToken.None);
                var filename = $"{package.Id}.{package.Version}.zip";
                var file = Path.Combine(downloadFolder, filename);
                var result = await downloader.CopyNupkgFileToAsync(file, CancellationToken.None);
                Console.WriteLine($"Download result ({filename}): {result}");
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

