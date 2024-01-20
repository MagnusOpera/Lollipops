namespace Lollipops;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGet.Packaging;
using NuGet.ProjectManagement;
using System.Collections.Generic;
using System.Text.Json;

internal class LollipopsProject(string root) : FolderNuGetProject(root) {
    private const string LISTING_FILENAME = "lollipops.json";
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    internal record InstalledPackage {
        public required string Id { get; init; }
        public string? Version { get; init; }
    }


    public override async Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, DownloadResourceResult downloadResourceResult, INuGetProjectContext nuGetProjectContext, CancellationToken token) {
        var res = await base.InstallPackageAsync(packageIdentity, downloadResourceResult, nuGetProjectContext, token);
        if (res) {
            var installedPackages = ReadInstalledPackages();
            var newInstalledPackage = new InstalledPackage {
                Id = packageIdentity.Id,
                Version = packageIdentity.Version?.ToString()
            };

            installedPackages.Add(newInstalledPackage);

            WriteInstalledPackages(installedPackages);
        }

        return res;
    }

    public override async Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token) {
        var res = await base.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

        var installedPackages = ReadInstalledPackages();
        installedPackages.RemoveAll(x => x.Id == packageIdentity.Id && x.Version == packageIdentity.Version?.ToString());

        return res;
    }

    private List<InstalledPackage> ReadInstalledPackages() {
        var listing = Path.Combine(Root, LISTING_FILENAME);
        if (!File.Exists(listing)) {
            return [];
        }

        var content = File.ReadAllText(listing);
        var installedPackages = JsonSerializer.Deserialize<List<InstalledPackage>>(content)!;
        return installedPackages;
    }

    private void WriteInstalledPackages(List<InstalledPackage> installedPackages) {
        var listing = Path.Combine(Root, LISTING_FILENAME);
        var content = JsonSerializer.Serialize(installedPackages, _options);
        File.WriteAllText(listing, content);
    }

    public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token) {
        var installedPackages = ReadInstalledPackages();
        var packageRefs = new List<PackageReference>();
        foreach (var installedPackage in installedPackages) {
            _ = NuGetVersion.TryParse(installedPackage.Version, out var version);
            var packageId = new PackageIdentity(installedPackage.Id, version);
            var packageRef = new PackageReference(packageId, NuGetFramework.AnyFramework);
            packageRefs.Add(packageRef);
        }

        return Task.FromResult(packageRefs.AsEnumerable());
    }
}
