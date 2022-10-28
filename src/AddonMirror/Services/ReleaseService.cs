using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AddonMirror.Extensions;
using AddonMirror.Models;
using Octokit;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FileMode = System.IO.FileMode;

namespace AddonMirror.Services;

public class ReleaseService : IReleaseService
{
    private const string TreeModeFile = "100644";
    private const string TreeModeExecutable = "100755";
    private const string TreeModeTree = "040000";
    private const string TreeModeSubmodule = "160000";
    private const string TreeModeSymLink = "120000";


    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubClient _gitHubClient;

    public ReleaseService(
        IHttpClientFactory httpClientFactory,
        IGitHubClient gitHubClient)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _gitHubClient = gitHubClient ?? throw new ArgumentNullException(nameof(gitHubClient));
    }

    public async Task<IEnumerable<Release>> GetUnmirroredReleasesAsync(
        Addon addon)
    {
        addon.ShouldNotBeNull(nameof(addon));

        var sourceReleases = await _gitHubClient.Repository.Release.GetAll(addon.SourceOwner, addon.SourceRepositoryName).ConfigureAwait(false);
        var mirrorReleases = await _gitHubClient.Repository.Release.GetAll(addon.MirrorOwner, addon.MirrorRepositoryName).ConfigureAwait(false);

        return sourceReleases.Where(x => !x.Prerelease && !mirrorReleases.Any(y => y.Name.Equals(x.Name)) && !addon.SkipReleases.Any(y => y.Equals(x.Name)));
    }

    public async Task CreateMirrorCommitAsync(
        Addon addon,
        Release unmirroredRelease)
    {
        addon.ShouldNotBeNull(nameof(addon));
        unmirroredRelease.ShouldNotBeNull(nameof(unmirroredRelease));

        // TODO: Check is there's already a pending PR.
        var discriminator = $"{Guid.NewGuid()}";
        var workDirectory = Path.Combine("_work", discriminator);
        Directory.CreateDirectory(workDirectory);

        var tempDirectory = Path.Combine(workDirectory, "temp");
        Directory.CreateDirectory(tempDirectory);

        using (var httpClient = this._httpClientFactory.CreateClient())
        {
            var sourceDirectory = Path.Combine(workDirectory, "source");
            Directory.CreateDirectory(sourceDirectory);

            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, unmirroredRelease.ZipballUrl);
            var productValue = new ProductInfoHeaderValue("AddonMirror", "1.0");
            var commentValue = new ProductInfoHeaderValue("(+https://aes.me)");

            httpRequestMessage.Headers.UserAgent.Add(productValue);
            httpRequestMessage.Headers.UserAgent.Add(commentValue);

            var response = await httpClient.SendAsync(httpRequestMessage);

            var sourceZipballName = $"source.zip";
            var sourceZipballFilePath = Path.Combine(tempDirectory, sourceZipballName);

            using (var fileStream = new FileStream(sourceZipballFilePath, FileMode.CreateNew))
            {
                await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
            }

            ZipFile.ExtractToDirectory(sourceZipballFilePath, sourceDirectory);

            var addonSourceDirectory = Directory.GetDirectories(sourceDirectory).First();

            foreach (var excludeFromSource in addon.SourceExclude)
            {
                var p = Path.Combine(addonSourceDirectory, excludeFromSource);

                if (Directory.Exists(p))
                {
                    Directory.Delete(p, true);
                }
            }

            // TODO: Enable configuration of pkg metadata file here
            var packageMetaFilePath = Path.Combine(addonSourceDirectory, "pkgmeta.yaml");

            if (File.Exists(packageMetaFilePath))
            {
                var yml = await File.ReadAllTextAsync(packageMetaFilePath).ConfigureAwait(false);

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(LowerCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var packageMetadata = deserializer.Deserialize<PackageMetadata>(yml);

                if (packageMetadata.Externals.Any())
                {
                    foreach (var item in packageMetadata.Externals)
                    {
                        var libraryDirectory = Path.Combine(addonSourceDirectory, item.Key);
                    }
                }
            }

            var mirrorMainBranch = await _gitHubClient.Repository.Branch.Get(addon.MirrorOwner, addon.MirrorRepositoryName, "main");
            var mirrorDevelopmentBranch = await _gitHubClient.Git.Reference.Create(addon.MirrorOwner, addon.MirrorRepositoryName, new NewReference($"refs/heads/{discriminator}", mirrorMainBranch.Commit.Sha));

            await ProcessFilesInPathAsync(mirrorOwner: addon.MirrorOwner, mirrorRepositoryName: addon.MirrorRepositoryName, mirrorDevelopmentBranchRef: mirrorDevelopmentBranch.Ref, sourceItemDirectory: addonSourceDirectory, treeRoot: $"source/{addon.Name}", treeSha: mirrorDevelopmentBranch.Object.Sha, commitSha: mirrorDevelopmentBranch.Object.Sha);

            var mirrorReleaseBranch = await _gitHubClient.Git.Reference.Create(addon.MirrorOwner, addon.MirrorRepositoryName, new NewReference($"refs/heads/releases/{unmirroredRelease.TagName}", mirrorMainBranch.Commit.Sha));

            await _gitHubClient.PullRequest.Create(addon.MirrorOwner, addon.MirrorRepositoryName, new NewPullRequest($"Automated Release: {addon.Name} {unmirroredRelease.Name}", mirrorDevelopmentBranch.Ref, mirrorReleaseBranch.Ref));
        }
    }

    private async Task<Commit?> ProcessFilesInPathAsync(
        string mirrorOwner,
        string mirrorRepositoryName,
        string mirrorDevelopmentBranchRef,
        string sourceItemDirectory,
        string treeRoot,
        string? treeSha = null,
        string? commitSha = null)
    {
        mirrorOwner.ShouldNotBeNullOrWhiteSpace(nameof(mirrorOwner));
        mirrorRepositoryName.ShouldNotBeNullOrWhiteSpace(nameof(mirrorRepositoryName));
        sourceItemDirectory.ShouldNotBeNullOrWhiteSpace(nameof(sourceItemDirectory));
        treeRoot.ShouldNotBeNullOrWhiteSpace(nameof(treeRoot));

        Commit? commit = null;
        var files = Directory.GetFiles(sourceItemDirectory, string.Empty, searchOption: SearchOption.TopDirectoryOnly);

        if (files.Any())
        {
            var contentTree = new NewTree()
            {
                BaseTree = treeSha
            };

            foreach (var sourceItemFilePath in files)
            {
                contentTree.Tree.Add(await ConstructContentTreeItemAsync(mirrorOwner, mirrorRepositoryName, sourceItemFilePath, sourceItemDirectory));
            }

            if (contentTree.Tree.Any())
            {
                var contentTreeResponse = await _gitHubClient.Git.Tree.Create(mirrorOwner, mirrorRepositoryName, contentTree);

                var directoryTree = new NewTree()
                {
                    BaseTree = commitSha
                };

                directoryTree.Tree.Add(new NewTreeItem()
                {
                    Mode = TreeModeTree,
                    Type = TreeType.Tree,
                    Path = $"{treeRoot}",
                    Sha = contentTreeResponse.Sha
                });

                var directoryTreeResponse = await _gitHubClient.Git.Tree.Create(mirrorOwner, mirrorRepositoryName, directoryTree);

                commit = await _gitHubClient.Git.Commit.Create(mirrorOwner, mirrorRepositoryName, new NewCommit($"Automated Commit for Content at: {treeRoot}", directoryTreeResponse.Sha, commitSha));

                if (commit != null)
                {
                    var update = await _gitHubClient.Git.Reference.Update(mirrorOwner, mirrorRepositoryName, mirrorDevelopmentBranchRef, new ReferenceUpdate(commit.Sha));
                    commitSha = update.Object.Sha;
                }
            }
        }

        foreach (var nestedDirectory in Directory.GetDirectories(sourceItemDirectory))
        {
            var updatedTreeRoot = $"{treeRoot}/{GetRelativePath(Path.GetFullPath(nestedDirectory), Path.GetFullPath(sourceItemDirectory))}";

            commit = await ProcessFilesInPathAsync(mirrorOwner: mirrorOwner, mirrorRepositoryName: mirrorRepositoryName, mirrorDevelopmentBranchRef, sourceItemDirectory: nestedDirectory, treeRoot: updatedTreeRoot, treeSha: null, commitSha: commitSha);

            if (commit != null)
            {
                commitSha = commit.Sha;
            }
        }

        return commit;
    }

    private async Task<NewTreeItem> ConstructContentTreeItemAsync(
        string mirrorOwner,
        string mirrorRepositoryName,
        string sourceItemFilePath,
        string sourceItemDirectory)
    {
        mirrorOwner.ShouldNotBeNullOrWhiteSpace(nameof(mirrorOwner));
        mirrorRepositoryName.ShouldNotBeNullOrWhiteSpace(nameof(mirrorRepositoryName));
        sourceItemFilePath.ShouldNotBeNullOrWhiteSpace(sourceItemFilePath);
        sourceItemDirectory.ShouldNotBeNullOrWhiteSpace(nameof(sourceItemDirectory));

        var content = await File.ReadAllTextAsync(sourceItemFilePath).ConfigureAwait(false);

        EncodingType encodingType;

        using (var stream = new FileStream(sourceItemFilePath, FileMode.Open, FileAccess.Read))
        {
            using (var reader = new BinaryReader(stream))
            {
                // read the first X bytes of the file
                // In this example I want to check if the file is a BMP
                // whose header is 424D in hex(2 bytes 6677)
                var code = reader.ReadByte().ToString() + reader.ReadByte().ToString();

                encodingType = EncodingType.Utf8;
            }
        }

        // https://stackoverflow.com/questions/11801983/how-to-create-a-commit-and-push-into-repo-with-github-api-v3/63461333#63461333
        var blob = await _gitHubClient.Git.Blob.Create(
            mirrorOwner,
            mirrorRepositoryName,
            new NewBlob
            {
                Content = content,
                Encoding = encodingType
            });

        return new NewTreeItem
        {
            Mode = TreeModeFile,
            Type = TreeType.Blob,
            Path = GetRelativePath(Path.GetFullPath(sourceItemFilePath), Path.GetFullPath(sourceItemDirectory)).Replace("\\", "/"),
            Sha = blob.Sha
        };
    }

    //public async Task UpdateMirrorAsync(
    //    AddonMirrorOptions configuration)
    //{
    //    var sourceReleases = await _gitHubClient.Repository.Release.GetAll(configuration.SourceOwner, configuration.SourceRepositoryName).ConfigureAwait(false);
    //    var mirrorReleases = await _gitHubClient.Repository.Release.GetAll(configuration.MirrorOwner, configuration.MirrorRepositoryName).ConfigureAwait(false);
    //    var missingReleases = sourceReleases.Where(x => !mirrorReleases.Any(y => y.Name.Equals(x.Name)) && !configuration.SkipReleases.Any(y => y.Equals(x.Name))).ToList();

    //    var names = missingReleases.Select(x => x.Name);
    //    var namesJson = JsonSerializer.Serialize(names, Constants.SerializerOptions);

    //    if (missingReleases.Any())
    //    {
    //        var workRootDirectory = Path.Combine("_work", configuration.SourceOwner, configuration.SourceRepositoryName);

    //        foreach (var missingRelease in missingReleases)
    //        {
    //            var releaseRootDirectory = Path.Combine(workRootDirectory, missingRelease.Name, Guid.NewGuid().ToString());

    //            if (!Directory.Exists(releaseRootDirectory))
    //            {
    //                Directory.CreateDirectory(releaseRootDirectory);
    //            }

    //            using (var httpClient = this._httpClientFactory.CreateClient())
    //            {
    //                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, missingRelease.ZipballUrl);
    //                var productValue = new ProductInfoHeaderValue("AddonPackager", "1.0");
    //                var commentValue = new ProductInfoHeaderValue("(+https://aes.me)");

    //                httpRequestMessage.Headers.UserAgent.Add(productValue);
    //                httpRequestMessage.Headers.UserAgent.Add(commentValue);

    //                var response = await httpClient.SendAsync(httpRequestMessage);

    //                var sourceZipballName = $"source.zip";
    //                var sourceZipballFileName = Path.Combine(releaseRootDirectory, sourceZipballName);

    //                using (var fileStream = new FileStream(sourceZipballFileName, FileMode.CreateNew))
    //                {
    //                    await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
    //                }

    //                var sourceRootDirectory = Path.Combine(releaseRootDirectory, "source");

    //                ZipFile.ExtractToDirectory(sourceZipballFileName, sourceRootDirectory);

    //                var releaseRepoMainBranch = await _gitHubClient.Repository.Branch.Get(configuration.MirrorOwner, configuration.MirrorRepositoryName, "main");

    //                // https://stackoverflow.com/questions/11801983/how-to-create-a-commit-and-push-into-repo-with-github-api-v3/63461333#63461333
    //                var blob = await _gitHubClient.Git.Blob.Create(
    //                    configuration.MirrorOwner,
    //                    configuration.MirrorRepositoryName,
    //                    new NewBlob
    //                    {
    //                        Content = "Howdy",
    //                        Encoding = EncodingType.Utf8
    //                    });

    //                var nt = new NewTree()
    //                {
    //                    BaseTree = releaseRepoMainBranch.Commit.Sha
    //                };

    //                nt.Tree.Add(new NewTreeItem()
    //                {
    //                    Mode = "100644",
    //                    Type = TreeType.Blob,
    //                    Path = "test.txt",
    //                    Sha = blob.Sha
    //                });

    //                var tree = await _gitHubClient.Git.Tree.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, nt);
    //                var commit = await _gitHubClient.Git.Commit.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewCommit("a", tree.Sha, new List<string>() { releaseRepoMainBranch.Commit.Sha }));
    //                var update = await _gitHubClient.Git.Reference.Update(configuration.MirrorOwner, configuration.MirrorRepositoryName, "refs/heads/main", new ReferenceUpdate(commit.Sha));

    //                var tag = await _gitHubClient.Git.Tag.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewTag
    //                {
    //                    Message = "test",
    //                    Object = commit.Sha,
    //                    Tag = missingRelease.TagName,
    //                    Type = TaggedType.Commit
    //                });

    //                var wowUpReleases = new WowUpReleases
    //                {
    //                    Releases = new List<WowUpRelease>()
    //                };

    //                var mirrorRelease = await _gitHubClient.Repository.Release.Create(configuration.MirrorOwner, configuration.MirrorRepositoryName, new NewRelease(missingRelease.TagName)
    //                {
    //                    Draft = true,
    //                    Name = missingRelease.Name,
    //                    TargetCommitish = commit.Sha
    //                });

    //                var mirrorRootDirectory = Path.Combine(releaseRootDirectory, "mirror");

    //                if (!Directory.Exists(mirrorRootDirectory))
    //                {
    //                    Directory.CreateDirectory(mirrorRootDirectory);
    //                }

    //                foreach (var variant in configuration.Variants)
    //                {
    //                    var variantReleaseName = $"{variant.Name}-{variant.Flavor}-{missingRelease.Name}";
    //                    var variantZipballName = $"{variantReleaseName}.zip";
    //                    var variantZipballFileName = Path.Combine(mirrorRootDirectory, variantZipballName);

    //                    var variantSourcePath = Path.Combine(sourceRootDirectory, variantReleaseName);

    //                    CopyDirectory(Directory.GetDirectories(sourceRootDirectory).First(), Path.Combine(variantSourcePath, variant.Name));

    //                    // TODO: Map commonly used hosted libraries OR include in source + check the libraries config in addon to ensure all are updated
    //                    CopyDirectory("../../../../../libs-src", Path.Combine(variantSourcePath, variant.Name, "libs"));

    //                    ZipFile.CreateFromDirectory(variantSourcePath, variantZipballFileName);

    //                    await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload(variantZipballName, "application/zip", File.OpenRead(variantZipballFileName), TimeSpan.FromMinutes(5)));

    //                    var metadata = new Dictionary<string, string>
    //                    {
    //                        { "flavor", variant.Flavor }
    //                    };

    //                    wowUpReleases.Releases.Add(new WowUpRelease
    //                    {
    //                        Name = variantReleaseName,
    //                        Version = missingRelease.Name,
    //                        Filename = variantZipballFileName,
    //                        NoLib = variant.NoLib,
    //                        Metadata = metadata
    //                    });
    //                }

    //                var wowUpReleaseFileName = Path.Combine(mirrorRootDirectory, "release.json");

    //                using (var fileStream = new FileStream(wowUpReleaseFileName, FileMode.CreateNew))
    //                {
    //                    var content = new UTF8Encoding(true).GetBytes(JsonSerializer.Serialize(wowUpReleases, Constants.SerializerOptions));
    //                    fileStream.Write(content, 0, content.Length);
    //                }

    //                await _gitHubClient.Repository.Release.UploadAsset(mirrorRelease, new ReleaseAssetUpload("release.json", "application/json", File.OpenRead(wowUpReleaseFileName), TimeSpan.FromMinutes(5)));

    //                await _gitHubClient.Repository.Release.Edit(configuration.MirrorOwner, configuration.MirrorRepositoryName, mirrorRelease.Id, new ReleaseUpdate()
    //                {
    //                    Draft = false
    //                });
    //            }

    //            Directory.Delete(releaseRootDirectory, true);
    //        }
    //    }
    //}

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

    public static string GetRelativePath(
        string fullPath,
        string containingFolder,
        bool mustBeInContainingFolder = false)
    {
        var file = new Uri(fullPath);

        if (containingFolder[containingFolder.Length - 1] != Path.DirectorySeparatorChar)
        {
            containingFolder += Path.DirectorySeparatorChar;
        }

        var folder = new Uri(containingFolder); // Must end in a slash to indicate folder

        var resp = folder.MakeRelativeUri(file);

        var relativePath =
            Uri.UnescapeDataString(
                folder.MakeRelativeUri(file)
                    .ToString()
                    .Replace('/', Path.DirectorySeparatorChar)
            );
        if (mustBeInContainingFolder && relativePath.IndexOf("..") == 0)
        {
            return null;
        }

        return relativePath;
    }
}
