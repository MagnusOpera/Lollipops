namespace Lollipops;

using System.Reflection;
using System.Runtime.Versioning;
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
using NuGet.Packaging.Signing;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.ComponentModel.Composition;

public record Package {
    public required string Id { get; init; }
    public string? Version { get; init; }
    public bool PreRelease { get; init; }
}


public class Container(CompositionContainer container) {
    public T Resolve<T>(string? name = null) {
        return container.GetExport<T>(name).Value;
    }
}


public class ContainerBuilder(AggregateCatalog aggregateCatalog) {
    public void Add(ComposablePartCatalog part) {
        aggregateCatalog.Catalogs.Add(part);
    }

    public Container Build() {
        var container = new CompositionContainer(aggregateCatalog);
        container.ComposeParts();

        return new Container(container);
    }    
}

public record Configuration {
    public required Package[] Packages { get; init; }
}



public class MachineWideSettings : IMachineWideSettings {
    private readonly Lazy<ISettings> _settings;

    public MachineWideSettings() {
        _settings = new Lazy<ISettings>(() => {
            var baseDirectory = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
            return global::NuGet.Configuration.Settings.LoadMachineWideSettings(baseDirectory);
        });
    }

    public ISettings Settings => _settings.Value;
}


public class ProjectContext : INuGetProjectContext {
    public PackageExtractionContext? PackageExtractionContext { get; set; } = null;

    public ISourceControlManagerProvider? SourceControlManagerProvider => null;

    public ExecutionContext? ExecutionContext => null;

    public XDocument? OriginalPackagesConfig { get; set; } = null;

    public NuGetActionType ActionType { get; set; }

    public Guid OperationId { get; set; }

    public void Log(MessageLevel level, string message, params object[] args) {
    }

    public void Log(ILogMessage message) {
    }

    public void ReportError(string message) {
    }

    public void ReportError(ILogMessage message) {
    }

    public FileConflictAction ResolveFileConflict(string message) {
        return FileConflictAction.Ignore;
    }
}


public static class ConfigurationExtensions {

    public static async Task<ContainerBuilder> Install(this Configuration configuration, string downloadFolder) {
        if (!Directory.Exists(downloadFolder)) {
            Directory.CreateDirectory(downloadFolder);
        }

        var logger = NullLogger.Instance;

        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3()); // Add v3 API s

        var project = new FolderNuGetProject(downloadFolder);
        var settings = Settings.LoadDefaultSettings(downloadFolder, null, new MachineWideSettings());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);
        var packageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, downloadFolder) {
            PackagesFolderNuGetProject = project,
        };

        var source = new PackageSource("https://api.nuget.org/v3/index.json");
        var repository = Repository.Factory.GetCoreV3(source);
        var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
        var findPackageResource = await repository.GetResourceAsync<FindPackageByIdResource>();
        using var sourceCacheContext = new SourceCacheContext();

        var targetFramework = Assembly.GetEntryAssembly()!.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        var frameworkNameProvider = new FrameworkNameProvider([DefaultFrameworkMappings.Instance], [DefaultPortableFrameworkMappings.Instance]);
        var nugetFramework = NuGetFramework.ParseFrameworkName(targetFramework!, frameworkNameProvider)!;

        var allCatalogs = new AggregateCatalog();
        foreach (var package in configuration.Packages) {
            var nugetCatalog = new AggregateCatalog();
            var nugetFiles = await installPackage(package);
            foreach (var file in nugetFiles) {
                var assemblyCatalog = new AssemblyCatalog(file);
                nugetCatalog.Catalogs.Add(assemblyCatalog);
            }

            allCatalogs.Catalogs.Add(nugetCatalog);
        }

        return new ContainerBuilder(allCatalogs);

        async Task<string[]> installPackage(Package package) {
            var metadata = await searchPackage(package)
                         ?? throw new ApplicationException($"Failed to resolve {package}");
            Console.WriteLine($"Successfully resolved {package} with version {metadata.Identity.Version}");

            var packageIds = await downloadPackage();

            var packageFilePath = project.GetInstalledPackageFilePath(metadata.Identity)
                                ?? throw new ApplicationException("Failed to find package files");
            using var archiveReader = new PackageArchiveReader(packageFilePath, null, null);
            var itemGroups = archiveReader.GetReferenceItems();
            var mostCompatibleFramework = new FrameworkReducer().GetNearest(nugetFramework, itemGroups.Select(x => x.TargetFramework))
                                        ?? throw new ApplicationException("Failed to find com files");
            Console.WriteLine($"Found {mostCompatibleFramework}");

            var mostCompatibleGroup = itemGroups.FirstOrDefault(i => i.TargetFramework == mostCompatibleFramework)
                                    ?? throw new ApplicationException("Failed to find compatible fx");

            var dependencyFiles = new List<string>();
            foreach (var packageId in packageIds) {
                var nugetPackagePath = project.GetInstalledPath(metadata.Identity);
                var nugetFiles = mostCompatibleGroup.Items.Select(item => Path.Combine(nugetPackagePath, item)).ToArray();
                dependencyFiles.AddRange(dependencyFiles);
            }

            return [.. dependencyFiles];

            async Task<IPackageSearchMetadata?> searchPackage(Package package) {
                if (package.Version is not null) {
                    // find exact version
                    if (!NuGetVersion.TryParse(package.Version, out var nugetVersion)) {
                        throw new ApplicationException($"Invalid version '{package.Version}' for package '{package.Id}'");
                    }

                    var packageIdentity = new PackageIdentity(package.Id, nugetVersion);
                    var packageMetadata = await packageMetadataResource.GetMetadataAsync(packageIdentity,
                                                                                        sourceCacheContext,
                                                                                        logger,
                                                                                        CancellationToken.None);
                    return packageMetadata;
                }

                // find latest version
                var results = await packageMetadataResource.GetMetadataAsync(package.Id,
                                                                            package.PreRelease,
                                                                            false,
                                                                            sourceCacheContext,
                                                                            logger,
                                                                            CancellationToken.None);
                return results.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            }

            async Task<PackageIdentity[]> downloadPackage() {
                if (!packageManager.PackageExistsInPackagesFolder(metadata.Identity, PackageSaveMode.None)) {
                    var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest,
                                                                  true,
                                                                  includeUnlisted: false,
                                                                  VersionConstraints.None);

                    var projectContext = new ProjectContext {
                        PackageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv2,
                                                                                XmlDocFileSaveMode.None,
                                                                                ClientPolicyContext.GetClientPolicy(settings, logger),
                                                                                logger)
                    };

                    var downloadContext = new PackageDownloadContext(resolutionContext.SourceCacheContext,
                                                                     downloadFolder,
                                                                     resolutionContext.SourceCacheContext.DirectDownload);

                    await packageManager.InstallPackageAsync(project,
                                                             metadata.Identity,
                                                             resolutionContext,
                                                             projectContext,
                                                             downloadContext,
                                                             [repository],
                                                             [],
                                                             CancellationToken.None);
                }

                var allPackages = await packageManager.GetInstalledPackagesInDependencyOrder(project, CancellationToken.None);
                return allPackages.ToArray();
            }
        }
    }
}

