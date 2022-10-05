using AddonPackager.Models;
using Octokit;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FileMode = System.IO.FileMode;

namespace AddonPackager.Services;

public class ReleaseService : IReleaseService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubClient _gitHubClient;

    public ReleaseService(
        IHttpClientFactory httpClientFactory,
        IGitHubClient gitHubClient)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
    }

    public async Task UpdateMirrorAsync(
        AddonPackagerConfiguration configuration)
    {
        var sourceReleases = await _gitHubClient.Repository.Release.GetAll(configuration.SourceOwner, configuration.SourceRepositoryName).ConfigureAwait(false);
        var mirrorReleases = await _gitHubClient.Repository.Release.GetAll(configuration.MirrorOwner, configuration.MirrorRepositoryName).ConfigureAwait(false);
        var missingReleases = sourceReleases.Where(x => !mirrorReleases.Any(y => y.Name.Equals(x.Name)) && !configuration.SkipReleases.Any(y => y.Equals(x.Name))).ToList();

        var names = sourceReleases.Select(x => x.Name);
        var namesJson = JsonSerializer.Serialize(names, AddonPackagerConstants.SerializerOptions);

        if (missingReleases.Any())
        {
            foreach (var missingRelease in missingReleases)
            {
                using (var httpClient = this._httpClientFactory.CreateClient())
                {
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, missingRelease.ZipballUrl);
                    var productValue = new ProductInfoHeaderValue("AddonPackager", "1.0");
                    var commentValue = new ProductInfoHeaderValue("(+https://aes.me)");

                    httpRequestMessage.Headers.UserAgent.Add(productValue);
                    httpRequestMessage.Headers.UserAgent.Add(commentValue);

                    var response = await httpClient.SendAsync(httpRequestMessage);

                    var downloadPath = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName, "download");

                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }

                    var sourceZipballFileName = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName, "download", $"{missingRelease.Name}.zip");

                    using (var fileStream = new FileStream(sourceZipballFileName, FileMode.CreateNew))
                    {
                        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                    }

                    var sourcePath = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName, "src");

                    ZipFile.ExtractToDirectory(sourceZipballFileName, sourcePath);

                    var releaseRepoMainBranch = await _gitHubClient.Repository.Branch.Get(configuration.MirrorOwner, configuration.MirrorRepositoryName, "main");

                    // https://stackoverflow.com/questions/11801983/how-to-create-a-commit-and-push-into-repo-with-github-api-v3/63461333#63461333
                    var blob = await _gitHubClient.Git.Blob.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName,
                        new NewBlob()
                        {
                            Content = "Howdy",
                            Encoding = EncodingType.Utf8
                        });

                    var nt = new NewTree()
                    {
                        BaseTree = releaseRepoMainBranch.Commit.Sha
                    };

                    nt.Tree.Add(new NewTreeItem()
                    {
                        Mode = "100644",
                        Type = TreeType.Blob,
                        Path = "test.txt",
                        Sha = blob.Sha
                    });

                    var tree = await _gitHubClient.Git.Tree.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, nt);

                    var commit = await _gitHubClient.Git.Commit.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewCommit("a", tree.Sha, new List<string>() { releaseRepoMainBranch.Commit.Sha }));

                    var update = await _gitHubClient.Git.Reference.Update(configuration.MirrorOwner, configuration.MirrorRepositoryName, "refs/heads/main", new ReferenceUpdate(commit.Sha));

                    var tag = await _gitHubClient.Git.Tag.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewTag
                    {
                        Message = "test",
                        Object = commit.Sha,
                        Tag = missingRelease.TagName,
                        Type = TaggedType.Commit
                    });

                    var wowUpReleases = new WowUpReleases
                    {
                        Releases = new List<WowUpRelease>()
                    };

                    var mirrorRelease = await _gitHubClient.Repository.Release.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewRelease(missingRelease.TagName)
                    {
                        Draft = true,
                        Name = missingRelease.Name,
                        TargetCommitish = commit.Sha
                    });

                    foreach (var variant in configuration.Variants)
                    {
                        var releasePath = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName, "release");

                        var variantReleaseName = $"{variant.Name}-{variant.Flavor}-{missingRelease.Name}";
                        var variantZipballName = $"{variantReleaseName}.zip";
                        var variantZipballFileName = Path.Combine(releasePath, variantZipballName);

                        var sourceRoot = Directory.GetDirectories(sourcePath).First();

                        if (!Directory.Exists(releasePath))
                        {
                            Directory.CreateDirectory(releasePath);
                        }

                        var variantSourcePath = Path.Combine(releasePath, variantReleaseName, variant.Name);

                        CopyDirectory(sourceRoot, variantSourcePath);

                        CopyDirectory("../../../../../libs-src", Path.Combine(variantSourcePath, "libs"));

                        ZipFile.CreateFromDirectory(Path.Combine(releasePath, variantReleaseName), variantZipballFileName);

                        await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload(variantZipballName, "application/zip", File.OpenRead(variantZipballFileName), TimeSpan.FromMinutes(5)));

                        var metadata = new Dictionary<string, string>();
                        metadata.Add("flavor", variant.Flavor);

                        wowUpReleases.Releases.Add(new WowUpRelease
                        {
                            Name = variantReleaseName,
                            Version = missingRelease.Name,
                            Filename = variantZipballFileName,
                            NoLib = variant.NoLib,
                            Metadata = metadata
                        });
                    }

                    var wowUpReleaseFileName = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName, "release", "release.json");

                    using (var fileStream = new FileStream(wowUpReleaseFileName, FileMode.CreateNew))
                    {
                        var content = new UTF8Encoding(true).GetBytes(JsonSerializer.Serialize(wowUpReleases, AddonPackagerConstants.SerializerOptions));
                        fileStream.Write(content, 0, content.Length);
                    }

                    await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload("release.json", "application/json", File.OpenRead(wowUpReleaseFileName), TimeSpan.FromMinutes(5)));

                    await _gitHubClient.Repository.Release.Edit(configuration.MirrorOwner, configuration.MirrorRepositoryName, mirrorRelease.Id, new ReleaseUpdate()
                    {
                        Draft = false
                    });
                }
            }
        }
    }

    private void CopyDirectory(string strSource, string strDestination)
    {
        if (!Directory.Exists(strDestination))
        {
            Directory.CreateDirectory(strDestination);
        }

        var dirInfo = new DirectoryInfo(strSource);
        var files = dirInfo.GetFiles();

        foreach (var tempfile in files)
        {
            tempfile.CopyTo(Path.Combine(strDestination, tempfile.Name));
        }

        var directories = dirInfo.GetDirectories();

        foreach (var tempdir in directories)
        {
            CopyDirectory(Path.Combine(strSource, tempdir.Name), Path.Combine(strDestination, tempdir.Name));
        }

    }
}
