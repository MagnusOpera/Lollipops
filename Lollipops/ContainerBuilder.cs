namespace Lollipops;

using System.Reflection;
using System.Runtime.Versioning;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.PackageManagement;
using NuGet.Resolver;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.ProjectManagement;

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


public static class ContainerBuilderExtensions {

    public static async Task<IContainerBuilder> Install(this Configuration configuration, string projectFolder) {
        var logger = NullLogger.Instance;

        Directory.Delete(projectFolder, true);

        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());

        var project = new FolderNuGetProject(projectFolder);
        var settings = Settings.LoadDefaultSettings(projectFolder, null, new MachineWideSettings());
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, providers);
        var packageManager = new NuGetPackageManager(sourceRepositoryProvider, settings, projectFolder) {
            PackagesFolderNuGetProject = project
        };

        var defaultRepository = getSourceRepository(null);
        var repository = getSourceRepository(configuration.Source);

        var packageMetadataResource = await repository.GetResourceAsync<PackageMetadataResource>();
        using var sourceCacheContext = new SourceCacheContext();

        // find host runtime
        var targetFramework = Assembly.GetEntryAssembly()!.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        var frameworkNameProvider = new FrameworkNameProvider([DefaultFrameworkMappings.Instance], [DefaultPortableFrameworkMappings.Instance]);
        var nugetFramework = NuGetFramework.ParseFrameworkName(targetFramework!, frameworkNameProvider)!;


        foreach (var package in configuration.Packages) {
            // Console.WriteLine($"Adding package '{package}");
            await installPackage(package);
        }

        var files = getProjectFiles();
        var projectCatalog = new AggregateCatalog();
        foreach (var file in files) {
            // Console.WriteLine($"\tAdding file '{file}");
            var assemblyCatalog = new AssemblyCatalog(file);
            projectCatalog.Catalogs.Add(assemblyCatalog);
        }

        return new ContainerBuilder(projectCatalog);



        SourceRepository getSourceRepository(string? url) {
            if (url is null) {
                return sourceRepositoryProvider.GetRepositories().First();
            } else {
                return new SourceRepository(new PackageSource(url), providers);
            }
        }


        string[] getProjectFiles() {
            var packageIds = LocalFolderUtility.GetPackagesV2(projectFolder, logger).Select(x => x.Identity).ToArray();

            var dependencyFiles = new List<string>();
            foreach (var packageId in packageIds) {
                var packageFilePath = project.GetInstalledPackageFilePath(packageId);
                using var archiveReader = new PackageArchiveReader(packageFilePath, null, null);
                var itemGroups = archiveReader.GetReferenceItems();
                var mostCompatibleFramework = new FrameworkReducer().GetNearest(nugetFramework, itemGroups.Select(x => x.TargetFramework));

                var mostCompatibleGroup = itemGroups.FirstOrDefault(i => i.TargetFramework == mostCompatibleFramework)
                                        ?? throw new Exception($"Package '{packageId}' is not compatible with {mostCompatibleFramework}");

                var nugetPackagePath = project.GetInstalledPath(packageId);
                var nugetFiles = mostCompatibleGroup.Items.Select(item => Path.Combine(nugetPackagePath, item)).ToArray();
                dependencyFiles.AddRange(nugetFiles);
            }

            return [.. dependencyFiles];
        }


        // Task<string[]> getPackageFiles(PackageIdentity packageId) {
        //     var allPackageIds = LocalFolderUtility.GetPackagesV2(downloadFolder, logger).Select(x => x.Identity).ToArray();

        //     var dependencyFiles = new List<string>();
        //     var packageFilePath = project.GetInstalledPackageFilePath(packageId);
        //     using var archiveReader = new PackageArchiveReader(packageFilePath, null, null);
        //     var itemGroups = archiveReader.GetReferenceItems();
        //     var mostCompatibleFramework = new FrameworkReducer().GetNearest(nugetFramework, itemGroups.Select(x => x.TargetFramework));

        //     var mostCompatibleGroup = itemGroups.FirstOrDefault(i => i.TargetFramework == mostCompatibleFramework)
        //                             ?? throw new Exception($"Package '{packageId}' is not compatible with {mostCompatibleFramework}");

        //     var nugetPackagePath = project.GetInstalledPath(packageId);
        //     var nugetFiles = mostCompatibleGroup.Items.Select(item => Path.Combine(nugetPackagePath, item)).ToArray();
        //     dependencyFiles.AddRange(nugetFiles);

        //     return Task.FromResult(dependencyFiles.ToArray());
        // }



        async Task installPackage(Package package) {
            var metadata = await searchPackage(package)
                         ?? throw new Exception($"Package '{package}' is not available");

            await downloadPackage();


            async Task<IPackageSearchMetadata?> searchPackage(Package package) {
                if (package.Version is not null) {
                    // find exact version
                    if (!NuGetVersion.TryParse(package.Version, out var nugetVersion)) {
                        throw new Exception($"Invalid version '{package.Version}' for package '{package.Id}'");
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



            async Task downloadPackage() {
                if (!packageManager.PackageExistsInPackagesFolder(metadata.Identity, PackageSaveMode.Defaultv2)) {
                    var resolutionContext = new ResolutionContext(DependencyBehavior.Lowest,
                                                                  true,
                                                                  false,
                                                                  VersionConstraints.None,
                                                                  new GatherCache(),
                                                                  new SourceCacheContext
                                                                  {
                                                                      NoCache = false,
                                                                      DirectDownload = false
                                                                  });

                    var projectContext = new ProjectContext {
                        PackageExtractionContext = new PackageExtractionContext(PackageSaveMode.Defaultv2,
                                                                                XmlDocFileSaveMode.None,
                                                                                ClientPolicyContext.GetClientPolicy(settings, logger),
                                                                                logger)
                    };

                    var downloadContext = new PackageDownloadContext(resolutionContext.SourceCacheContext,
                                                                     projectFolder,
                                                                     resolutionContext.SourceCacheContext.DirectDownload);

                    await packageManager.InstallPackageAsync(project,
                                                             metadata.Identity,
                                                             resolutionContext,
                                                             projectContext,
                                                             downloadContext,
                                                             [repository],
                                                             [defaultRepository],
                                                             CancellationToken.None);
                }
            }
        }
    }
}
