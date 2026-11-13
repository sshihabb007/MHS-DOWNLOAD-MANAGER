using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ShihabMehediIDM
{
    public enum ShihabAppInstallMode
    {
        Portable = 0,
        InstalledSetup = 1
    }

    [DataContract]
    public sealed class ShihabGitHubAssetDto
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "browser_download_url")] public string BrowserDownloadUrl { get; set; }
        [DataMember(Name = "content_type")] public string ContentType { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }
    }

    [DataContract]
    public sealed class ShihabGitHubReleaseDto
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "body")] public string Body { get; set; }
        [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
        [DataMember(Name = "published_at")] public string PublishedAt { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public List<ShihabGitHubAssetDto> Assets { get; set; }
    }

    public sealed class ShihabUpdateCheckResult
    {
        public bool Ok { get; set; }
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string TagName { get; set; }
        public string ReleaseTitle { get; set; }
        public string ReleaseNotes { get; set; }
        public string HtmlUrl { get; set; }
        public string PublishedAt { get; set; }
        public bool IsPrerelease { get; set; }
        public ShihabAppInstallMode InstallMode { get; set; }
        public ShihabGitHubAssetDto PrimaryAsset { get; set; }
        public ShihabGitHubAssetDto ExtensionZipAsset { get; set; }
        public ShihabGitHubAssetDto Sha256SumsAsset { get; set; }
        public string ExpectedPrimarySha256 { get; set; }
        public string ExpectedExtensionSha256 { get; set; }
        public string ErrorMessage { get; set; }

        public ShihabUpdateCheckResult()
        {
            CurrentVersion = ShihabAutoUpdater.GetCurrentAppVersion();
            LatestVersion = CurrentVersion;
            TagName = "";
            ReleaseTitle = "";
            ReleaseNotes = "";
            HtmlUrl = "https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases";
            PublishedAt = "";
            ExpectedPrimarySha256 = "";
            ExpectedExtensionSha256 = "";
            ErrorMessage = "";
        }
    }
}

