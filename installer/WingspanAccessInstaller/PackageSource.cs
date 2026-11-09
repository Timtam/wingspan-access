using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace WingspanAccessInstaller;

/// <summary>A package ZIP; a downloaded one is deleted when disposed.</summary>
internal sealed class Package : IDisposable
{
    private readonly string _downloadDir;

    public Package(string zip, string downloadDir = null)
    {
        Zip = zip;
        _downloadDir = downloadDir;
    }

    public string Zip { get; }

    public void Dispose()
    {
        if (_downloadDir != null)
            GameInstall.TryDeleteDirectory(_downloadDir);
    }
}

/// <summary>Finds the package to install.</summary>
internal static class PackageSource
{
    private const string Prefix = "WingspanAccess-";

    /// <summary>
    /// The ZIP from the command line; otherwise the newer of the release's package (downloaded) and
    /// a WingspanAccess-x.y.z.zip next to the installer.
    /// </summary>
    public static Package Get(string zipPath, ReleaseInfo release, Action<int> progress, CancellationToken token)
    {
        if (!string.IsNullOrEmpty(zipPath))
        {
            if (!File.Exists(zipPath))
                throw new InstallerException(Strings.Get("no_package", zipPath), 2);
            Log.Info(Strings.Get("using_package", Path.GetFileName(zipPath)));
            return new Package(zipPath);
        }

        (string localZip, Version localVersion) = FindLocal();
        if (release?.Package != null && (localZip == null || release.Version == null || release.Version > localVersion))
        {
            string dir = GameInstall.NewTempDir("WingspanAccessDownload");
            string target = Path.Combine(dir, release.Package.Name);
            Log.Info(Strings.Get("downloading", release.Package.Name));
            try
            {
                Releases.Download(release.Package.DownloadUrl, target, progress, token);
            }
            catch
            {
                GameInstall.TryDeleteDirectory(dir);
                throw;
            }

            return new Package(target, dir);
        }

        if (localZip != null)
        {
            Log.Info(Strings.Get("using_package", Path.GetFileName(localZip)));
            return new Package(localZip);
        }

        throw new InstallerException(Strings.Get("no_package", Strings.Get("no_package_local")));
    }

    private static (string Zip, Version Version) FindLocal()
    {
        string dir = Path.GetDirectoryName(Program.ExePath);
        return Directory.GetFiles(dir, Prefix + "*.zip")
            .Where(file => Releases.PackageName.IsMatch(Path.GetFileName(file)))
            .Select(file => (Zip: file, Version: Releases.ParseVersion(Path.GetFileNameWithoutExtension(file).Substring(Prefix.Length))))
            .OrderByDescending(found => found.Version)
            .FirstOrDefault();
    }
}
