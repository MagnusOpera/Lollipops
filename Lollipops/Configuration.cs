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
using NuGet.Packaging.Signing;
using System.ComponentModel.Composition.Hosting;

public record Configuration {
    public required Package[] Packages { get; init; }
}

public static class ConfigurationExtensions {

    public static async Task<IContainerBuilder> Install(this Configuration configuration, string downloadFolder) {
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
            PackagesFolderNuGetProject = project
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

            var packageIds = await downloadPackage();

            var packageFilePath = project.GetInstalledPackageFilePath(metadata.Identity);
            using var archiveReader = new PackageArchiveReader(packageFilePath, null, null);
            var itemGroups = archiveReader.GetReferenceItems();
            var mostCompatibleFramework = new FrameworkReducer().GetNearest(nugetFramework, itemGroups.Select(x => x.TargetFramework));

            var mostCompatibleGroup = itemGroups.FirstOrDefault(i => i.TargetFramework == mostCompatibleFramework)
                                    ?? throw new ApplicationException($"Package {package.Id} is not compatible with {mostCompatibleFramework}");

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

                // returns all dependencies
                var allPackages = await packageManager.GetInstalledPackagesInDependencyOrder(project, CancellationToken.None);
                return allPackages.ToArray();
            }
        }
    }
}
