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
using NuGet.Packaging.Signing;

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
    public PackageExtractionContext PackageExtractionContext { get; set; } = null!;

    public ISourceControlManagerProvider SourceControlManagerProvider => null!;

    public ExecutionContext ExecutionContext => null!;

    public XDocument OriginalPackagesConfig { get; set; } = null!;

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

    public static async Task Install(this Configuration configuration, string downloadFolder) {
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

        foreach (var package in configuration.Packages) {
            var metadata = await searchPackage(package);
            if (metadata is null) {
                Console.WriteLine($"Failed to resolve {package}");
            } else {
                Console.WriteLine($"Successfully resolved {package} with version {metadata.Identity.Version}");

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

                var packageFilePath = project.GetInstalledPackageFilePath(metadata.Identity);
                if (packageFilePath is null) {
                    Console.WriteLine("Failed to find package files");
                } else {
                    using var archiveReader = new PackageArchiveReader(packageFilePath, null, null);
                    var itemGroups = archiveReader.GetReferenceItems();
                    var mostCompatibleFramework = new FrameworkReducer().GetNearest(nugetFramework, itemGroups.Select(x => x.TargetFramework));
                    if (mostCompatibleFramework is null) {
                        Console.WriteLine("Failed to find com files");
                    } else {
                        Console.WriteLine($"Found {mostCompatibleFramework}");

                        var mostCompatibleGroup = itemGroups.FirstOrDefault(i => i.TargetFramework == mostCompatibleFramework);
                        if (mostCompatibleGroup is null) {
                            Console.WriteLine("Failed to find compatible fx");
                        } else {
                            var nugetPackagePath = project.GetInstalledPath(metadata.Identity);
                            foreach(var item in mostCompatibleGroup.Items) {
                                var sourceAssemblyPath = Path.Combine(nugetPackagePath, item);
                                var assemblyName = Path.GetFileName(sourceAssemblyPath);
                                Console.WriteLine($"Assembly: {assemblyName}");
                            }
                        }
                    }
                }
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

