﻿using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Octokit;
using Octokit.Tests.Integration;
using Xunit;

public class PullRequestsClientTests : IDisposable
{
    readonly IGitHubClient _client;
    readonly IPullRequestsClient _pullRequestsClient;
    readonly Repository _repository;

    public PullRequestsClientTests()
    {
        _client = new GitHubClient(new ProductHeaderValue("OctokitTests"))
        {
            Credentials = Helper.Credentials
        };

        _pullRequestsClient = _client.Repository.PullRequest;

        var repoName = Helper.MakeNameWithTimestamp("source-repo");

        _repository = _client.Repository.Create(new NewRepository { Name = repoName, AutoInit = true }).Result;
    }

    [IntegrationTest]
    public async Task CanCreate()
    {
        // create new commit for master branch
        var newMasterTree = await CreateTree(new Dictionary<string, string> { { "README.md", "Hello World!" } });
        var newMaster = await CreateCommit("baseline for pull request", newMasterTree.Sha);
        // update master
        await _client.GitDatabase.Reference.Update(Helper.UserName, _repository.Name, "heads/master", new ReferenceUpdate(newMaster.Sha, true));

        // create new commit for feature branch
        var featureBranchTree = await CreateTree(new Dictionary<string, string> { { "README.md", "I am overwriting this blob with something new" } });
        var newFeature = await CreateCommit("this is the commit to merge into the pull request", featureBranchTree.Sha);

        // create branch
        await _client.GitDatabase.Reference.Create(Helper.UserName, _repository.Name, new NewReference("refs/heads/my-branch", newFeature.Sha));

        // create pull request
        var head = Helper.UserName + ":my-branch";
        var newPullRequest = new NewPullRequest("a pull request", head, "master");
        var result = await _pullRequestsClient.Create(Helper.UserName, _repository.Name, newPullRequest);

        Assert.Equal("a pull request", result.Title);
    }

    async Task<TreeResponse> CreateTree(IDictionary<string,string> treeContents)
    {
        var collection = new List<NewTreeItem>();

        foreach (var c in treeContents)
        {
            var baselineBlob = new NewBlob
            {
                Content = c.Value,
                Encoding = EncodingType.Utf8
            };
            var baselineBlobResult = await _client.GitDatabase.Blob.Create(Helper.UserName, _repository.Name, baselineBlob);

            collection.Add(new NewTreeItem
            {
                Type = TreeType.Blob,
                Mode = FileMode.File,
                Path = c.Key,
                Sha = baselineBlobResult.Sha
            });
        }

        var newTree = new NewTree { Tree = collection };

        return await _client.GitDatabase.Tree.Create(Helper.UserName, _repository.Name, newTree);
    }

    async Task<Commit> CreateCommit(string message, string sha)
    {
        var newCommit = new NewCommit(message, sha);
        return await _client.GitDatabase.Commit.Create(Helper.UserName, _repository.Name, newCommit);
    }

    public void Dispose()
    {
        Helper.DeleteRepo(_repository);
    }
}
