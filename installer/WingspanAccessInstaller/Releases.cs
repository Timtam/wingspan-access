using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WingspanAccessInstaller;

[DataContract]
internal sealed class ReleaseAsset
{
    [DataMember(Name = "name")] public string Name { get; set; }
    [DataMember(Name = "browser_download_url")] public string DownloadUrl { get; set; }
    [DataMember(Name = "size")] public long Size { get; set; }
}

[DataContract]
internal sealed class ReleaseInfo
{
    [DataMember(Name = "tag_name")] public string Tag { get; set; }
    [DataMember(Name = "html_url")] public string PageUrl { get; set; }
    [DataMember(Name = "assets")] public ReleaseAsset[] Assets { get; set; }

    public Version Version => Releases.ParseVersion(Tag);

    /// <summary>The tag without the leading "v", for display.</summary>
    public string VersionText => Tag?.TrimStart('v', 'V');

    /// <summary>The mod package (WingspanAccess-x.y.z.zip).</summary>
    public ReleaseAsset Package => Assets?.FirstOrDefault(asset => Releases.PackageName.IsMatch(asset.Name ?? string.Empty));

    /// <summary>The installer EXE.</summary>
    public ReleaseAsset Installer => Assets?.FirstOrDefault(asset =>
        asset.Name != null &&
        asset.Name.StartsWith("WingspanAccessInstaller", StringComparison.OrdinalIgnoreCase) &&
        asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
}

/// <summary>The latest release on GitHub and downloads from it.</summary>
internal static class Releases
{
    public static readonly Regex PackageName = new(@"^WingspanAccess-[\w.\-]+\.zip$", RegexOptions.IgnoreCase);

    /// <summary>The newest published release of the repository ("owner/name"); null if there is none yet.</summary>
    public static ReleaseInfo Latest(string repository)
    {
        string url = $"https://api.github.com/repos/{repository}/releases/latest";
        Log.Detail($"GET {url}");
        using HttpClient client = NewClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using HttpResponseMessage response = client.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            Log.Detail("No release yet");
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] json = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        using var stream = new MemoryStream(json);
        var release = (ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(stream);
        string assets = string.Join(", ", release.Assets?.Select(asset => asset.Name) ?? Enumerable.Empty<string>());
        Log.Detail($"Latest release {release.Tag}, assets: {assets}");
        return release;
    }

    /// <summary>Downloads to a .part file first, so an interrupted download never looks complete.</summary>
    public static void Download(string url, string path, Action<int> progress, CancellationToken token)
    {
        Log.Detail($"Download {url}");
        string partial = path + ".part";
        using (HttpClient client = NewClient())
        using (HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).GetAwaiter().GetResult())
        {
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            using Stream source = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using FileStream target = File.Create(partial);
            var buffer = new byte[81920];
            long done = 0;
            int lastPercent = -1;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                token.ThrowIfCancellationRequested();
                target.Write(buffer, 0, read);
                done += read;
                if (total > 0)
                {
                    int percent = (int)(done * 100 / total.Value);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Invoke(percent);
                    }
                }
            }
        }

        if (File.Exists(path))
            File.Delete(path);
        File.Move(partial, path);
        Log.Detail($"Saved {path} ({new FileInfo(path).Length} bytes)");
    }

    /// <summary>Problems that mean "GitHub could not be asked", not a bug.</summary>
    public static bool IsCheckFailure(Exception e) =>
        e is HttpRequestException || e is TaskCanceledException || e is WebException || e is IOException || e is SerializationException;

    /// <summary>"v1.2.3", "1.2.3-beta" or "1.2.3+abc" as a four-part Version; null if it is none.</summary>
    public static Version ParseVersion(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        string core = text.Trim().TrimStart('v', 'V');
        int cut = core.IndexOfAny(new[] { '-', '+', ' ' });
        if (cut >= 0)
            core = core.Substring(0, cut);
        if (core.IndexOf('.') < 0)
            core += ".0";
        if (!Version.TryParse(core, out Version version))
            return null;
        // "1.2" and "1.2.0.0" must compare equal.
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    private static HttpClient NewClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WingspanAccessInstaller/{BuildInfo.Version}");
        return client;
    }
}
