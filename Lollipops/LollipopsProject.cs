namespace Lollipops;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System.Collections.Generic;
using System.Text.Json;

internal class LollipopsProject : FolderNuGetProject {
    private const string LISTING_FILENAME = "lollipops.json";
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    internal record InstalledPackage {
        public required string Id { get; init; }
        public string? Version { get; init; }
    }

    internal record ProjectConfiguration {
        public required HashSet<Package> RequestedPackages { get; init; }
        public required HashSet<InstalledPackage> InstalledPackages { get; init; }
    }

    public LollipopsProject(string root, HashSet<Package> packages) : base(root) {
        var configuration = ReadConfiguration();
        if (!configuration.RequestedPackages.SetEquals(packages)) {
            Directory.Delete(root, true);
            Directory.CreateDirectory(root);            
            configuration = configuration with { InstalledPackages = [] };
        }

        configuration = configuration with { RequestedPackages = packages, InstalledPackages = [] };
        WriteConfiguration(configuration);
    }



    public override async Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token) {
        var res = await base.InstallPackageAsync(packageIdentity, downloadResourceResult, nuGetProjectContext, token);
        if (res) {
            var newPackage = new InstalledPackage {
                Id = packageIdentity.Id,
                Version = packageIdentity.Version?.ToString()
            };

            var configuration = ReadConfiguration();
            configuration = configuration with { InstalledPackages = [.. configuration.InstalledPackages, newPackage] };
            WriteConfiguration(configuration);
        }

        return res;
    }

    public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token) {
        var res = await base.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

        var configuration = ReadConfiguration();
        var installedPackage = new InstalledPackage { Id = packageIdentity.Id, Version = packageIdentity.Version?.ToString() };
        configuration.InstalledPackages.Remove(installedPackage);
        WriteConfiguration(configuration);

        return res;
    }

    private ProjectConfiguration ReadConfiguration() {
        var listing = Path.Combine(Root, LISTING_FILENAME);
        if (!File.Exists(listing)) {
            return new ProjectConfiguration {
                InstalledPackages = [],
                RequestedPackages = []
            };
        }

        var content = File.ReadAllText(listing);
        var installedPackages = JsonSerializer.Deserialize<ProjectConfiguration>(content)!;
        return installedPackages;
    }

    private void WriteConfiguration(ProjectConfiguration configuration) {
        var listing = Path.Combine(Root, LISTING_FILENAME);
        var content = JsonSerializer.Serialize(configuration, _options);
        File.WriteAllText(listing, content);
    }

    public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token) {
        var installedPackages = ReadConfiguration();
        var packageRefs = new List<PackageReference>();
        foreach (var installedPackage in installedPackages.InstalledPackages) {
            _ = NuGetVersion.TryParse(installedPackage.Version, out var version);
            var packageId = new PackageIdentity(installedPackage.Id, version);
            var packageRef = new PackageReference(packageId, NuGetFramework.AnyFramework);
            packageRefs.Add(packageRef);
        }

        return Task.FromResult(packageRefs.AsEnumerable());
    }
}
