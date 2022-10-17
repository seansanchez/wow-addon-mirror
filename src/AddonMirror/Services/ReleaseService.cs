using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AddonMirror.Models;
using Octokit;
using FileMode = System.IO.FileMode;

namespace AddonMirror.Services;

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
        AddonMirrorConfiguration configuration)
    {
        var sourceReleases = await _gitHubClient.Repository.Release.GetAll(configuration.SourceOwner, configuration.SourceRepositoryName).ConfigureAwait(false);
        var mirrorReleases = await _gitHubClient.Repository.Release.GetAll(configuration.MirrorOwner, configuration.MirrorRepositoryName).ConfigureAwait(false);
        var missingReleases = sourceReleases.Where(x => !mirrorReleases.Any(y => y.Name.Equals(x.Name)) && !configuration.SkipReleases.Any(y => y.Equals(x.Name))).ToList();

        var names = missingReleases.Select(x => x.Name);
        var namesJson = JsonSerializer.Serialize(names, Constants.SerializerOptions);

        if (missingReleases.Any())
        {
            var workRootDirectory = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName);

            foreach (var missingRelease in missingReleases)
            {
                var releaseRootDirectory = Path.Combine(workRootDirectory, missingRelease.Name, Guid.NewGuid().ToString());

                if (!Directory.Exists(releaseRootDirectory))
                {
                    Directory.CreateDirectory(releaseRootDirectory);
                }

                using (var httpClient = this._httpClientFactory.CreateClient())
                {
                    var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, missingRelease.ZipballUrl);
                    var productValue = new ProductInfoHeaderValue("AddonPackager", "1.0");
                    var commentValue = new ProductInfoHeaderValue("(+https://aes.me)");

                    httpRequestMessage.Headers.UserAgent.Add(productValue);
                    httpRequestMessage.Headers.UserAgent.Add(commentValue);

                    var response = await httpClient.SendAsync(httpRequestMessage);

                    var sourceZipballName = $"source.zip";
                    var sourceZipballFileName = Path.Combine(releaseRootDirectory, sourceZipballName);

                    using (var fileStream = new FileStream(sourceZipballFileName, FileMode.CreateNew))
                    {
                        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                    }

                    var sourceRootDirectory = Path.Combine(releaseRootDirectory, "source");

                    ZipFile.ExtractToDirectory(sourceZipballFileName, sourceRootDirectory);

                    var releaseRepoMainBranch = await _gitHubClient.Repository.Branch.Get(configuration.MirrorOwner, configuration.MirrorRepositoryName, "main");

                    // https://stackoverflow.com/questions/11801983/how-to-create-a-commit-and-push-into-repo-with-github-api-v3/63461333#63461333
                    var blob = await _gitHubClient.Git.Blob.Create(
                        configuration.MirrorOwner,
                        configuration.MirrorRepositoryName,
                        new NewBlob
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

                    var mirrorRootDirectory = Path.Combine(releaseRootDirectory, "mirror");

                    if (!Directory.Exists(mirrorRootDirectory))
                    {
                        Directory.CreateDirectory(mirrorRootDirectory);
                    }

                    foreach (var variant in configuration.Variants)
                    {
                        var variantReleaseName = $"{variant.Name}-{variant.Flavor}-{missingRelease.Name}";
                        var variantZipballName = $"{variantReleaseName}.zip";
                        var variantZipballFileName = Path.Combine(mirrorRootDirectory, variantZipballName);

                        var variantSourcePath = Path.Combine(sourceRootDirectory, variantReleaseName);

                        CopyDirectory(Directory.GetDirectories(sourceRootDirectory).First(), Path.Combine(variantSourcePath, variant.Name));

                        // TODO: Map commonly used hosted libraries OR include in source + check the libraries config in addon to ensure all are updated
                        CopyDirectory("../../../../../libs-src", Path.Combine(variantSourcePath, variant.Name, "libs"));

                        ZipFile.CreateFromDirectory(variantSourcePath, variantZipballFileName);

                        await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload(variantZipballName, "application/zip", File.OpenRead(variantZipballFileName), TimeSpan.FromMinutes(5)));

                        var metadata = new Dictionary<string, string>
                        {
                            { "flavor", variant.Flavor }
                        };

                        wowUpReleases.Releases.Add(new WowUpRelease
                        {
                            Name = variantReleaseName,
                            Version = missingRelease.Name,
                            Filename = variantZipballFileName,
                            NoLib = variant.NoLib,
                            Metadata = metadata
                        });
                    }

                    var wowUpReleaseFileName = Path.Combine(mirrorRootDirectory, "release.json");

                    using (var fileStream = new FileStream(wowUpReleaseFileName, FileMode.CreateNew))
                    {
                        var content = new UTF8Encoding(true).GetBytes(JsonSerializer.Serialize(wowUpReleases, Constants.SerializerOptions));
                        fileStream.Write(content, 0, content.Length);
                    }

                    await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload("release.json", "application/json", File.OpenRead(wowUpReleaseFileName), TimeSpan.FromMinutes(5)));

                    await _gitHubClient.Repository.Release.Edit(configuration.MirrorOwner, configuration.MirrorRepositoryName, mirrorRelease.Id, new ReleaseUpdate()
                    {
                        Draft = false
                    });
                }

                Directory.Delete(releaseRootDirectory, true);
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
