global using GitHub = Pulumi.Github;
global using Azure = Pulumi.Azure;
global using AzureAD = Pulumi.AzureAD;

using GitHubManagement;
using GitHubManagement.Models;
using GitHubManagement.Serialization;
using Pulumi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NJsonSchema.Generation;
using Newtonsoft.Json.Serialization;
using NJsonSchema;
using System.IO;
using Pulumi.Github.Inputs;
using System.Text;
using Pulumi.Github;
using Newtonsoft.Json;

namespace GitHubManagement;

internal class Program
{
    static Task<int> Main() => Deployment.RunAsync(() =>
    {
        var config = new Pulumi.Config();

        var globalContext = new GlobalContext(config)
        {

        };

        var teamOptions = new TeamOptions()
        {
            TeamSlug = config.Require(ConfigKeys.TeamSlug),
            RepositoryFiles = new() { config.Require(ConfigKeys.ReposPath) }
        };

        Dictionary<string, RACRepositoryConfig> repos = new();

        foreach (var yamlFile in teamOptions.RepositoryFiles)
        {
            var fileRepos = YamlTool.GetRepositoriesFromFile(yamlFile);

            foreach (var p in fileRepos)
                repos.Add(p.Key, p.Value);
        }

        foreach (var (name, repo) in repos)
            CreateRepository(globalContext, teamOptions, name, repo);
    });

    static void CreateRepository(GlobalContext globalContext, TeamOptions teamOptions, string repoName, RACRepositoryConfig repoConfig)
    {
        if (string.Equals(repoConfig.visibility, "internal", StringComparison.InvariantCultureIgnoreCase))
        {
            // Free organization doesn't support internal
            repoConfig.visibility = "private";
        }

        var repoArgs = new GitHub.RepositoryArgs
        {
            Name = repoName,
            Description = repoConfig.description,
            Visibility = repoConfig.visibility, 
            HasIssues = repoConfig.has_issues,
            HasProjects = repoConfig.has_projects,
            HasWiki = repoConfig.has_wiki,
            IsTemplate = repoConfig.is_template,
            AllowMergeCommit = repoConfig.allow_merge_commit,
            AllowSquashMerge = repoConfig.allow_squash_merge,
            AllowRebaseMerge = repoConfig.allow_rebase_merge,
            AllowAutoMerge = repoConfig.allow_auto_merge,
            DeleteBranchOnMerge = repoConfig.delete_branch_on_merge,
            HasDownloads = repoConfig.has_downloads,
            AutoInit = repoConfig.auto_init,
            Archived = repoConfig.archived,
            ArchiveOnDestroy = repoConfig.archive_on_destroy,
            VulnerabilityAlerts = repoConfig.vulnerability_alerts,
            HomepageUrl = repoConfig.homepage_url,
            Topics = (repoConfig.topics ?? Enumerable.Empty<string>()).ToArray()
        };

        // Create a GitHub Repository
        var repository = new GitHub.Repository(repoName, repoArgs);
        RepositoryFile codeowners = null;

        var defaultBranch = new GitHub.BranchDefault($"{repoName}-default", new BranchDefaultArgs()
        {
            Repository = repository.Name,
            Branch = repoConfig.default_branch
        });

        if (repoConfig.Codeowner?.Create == true && repoConfig.Codeowner.Approvers?.Count > 0)
        {
            StringBuilder sb = new StringBuilder();

            foreach (var approver in repoConfig.Codeowner.Approvers)
            {
                // Ensures the team exists
                var team = globalContext.GetGitHubTeam(approver.Approver);

                sb.AppendLine($"# {approver.Comment}");
                sb.AppendLine($"{approver.Pattern}\t@{globalContext.GitHubOrganization}/{approver.Approver}");
            }

            var codeownerArgs = new RepositoryFileArgs()
            {
                Repository = repository.Name,
                File = "CODEOWNERS",
                Content = sb.ToString(),
                Branch = defaultBranch.Branch,
                CommitMessage = "Default approvals",
                CommitAuthor = "Automated",
                CommitEmail = "github@ractest.com.au",
                OverwriteOnCreate = true,
            };

            codeowners = new RepositoryFile($"{repoName}-CODEOWNER", codeownerArgs, new CustomResourceOptions()
            {
                IgnoreChanges = new() { nameof(RepositoryFile.Content), nameof(RepositoryFile.CommitAuthor), nameof(RepositoryFile.CommitMessage), nameof(RepositoryFile.CommitEmail) }
            });
        }


        if(repoConfig.teams == null || repoConfig.teams.Count == 0)
        {
            throw new InvalidOperationException("Repository must have at least one team access defined");
        }

        foreach (var (team, access) in repoConfig.teams)
        {
            var teamAccess = new GitHub.TeamRepository($"{repoName}-{team}", new TeamRepositoryArgs()
            {
                Permission = access,
                Repository = repository.Name,
                TeamId = globalContext.GetGitHubTeam(team).Id
            });
        }

        // Add default branch protection
        if(repoConfig.branch_protection == null || repoConfig.branch_protection.Count == 0)
        {
            repoConfig.branch_protection ??= new();

            repoConfig.branch_protection.Add(new()
            {
                Pattern = repoConfig.default_branch,
                EnforceAdmins = true,
                RequirePullRequestReviews = new()
                {
                   RequireCodeOwnerReviews = true,
                   RequiredApprovingReviewCount = 2                   
                }
            });
        }

        foreach (var branchProtection in repoConfig.branch_protection ?? new())
        {
            branchProtection.Pattern ??= repoConfig.default_branch;

            var options = new GitHub.BranchProtectionArgs()
            {
                RepositoryId = repository.Id,
                RequireSignedCommits = branchProtection.RequireSignedCommits,
                Pattern = branchProtection.Pattern,
                AllowsForcePushes = branchProtection.AllowsForcePushes,
                AllowsDeletions = branchProtection.AllowsDeletions,
                RequireConversationResolution = branchProtection.RequireConversationResolution,
                RequiredLinearHistory = branchProtection.RequiredLinearHistory,
                BlocksCreations = branchProtection.BlocksCreations,
                EnforceAdmins = branchProtection.EnforceAdmins,
            };

            if (branchProtection.RequirePullRequestReviews != null)
            {
                options.RequiredPullRequestReviews.Add(new BranchProtectionRequiredPullRequestReviewArgs()
                {
                    RestrictDismissals = branchProtection.RequirePullRequestReviews.RestrictDismissals,
                    DismissStaleReviews = branchProtection.RequirePullRequestReviews.DismissStaleReviews,
                    RequiredApprovingReviewCount = branchProtection.RequirePullRequestReviews.RequiredApprovingReviewCount,
                    RequireCodeOwnerReviews = branchProtection.RequirePullRequestReviews.RequireCodeOwnerReviews
                    // Not supported
                    //DismissalRestrictions = branchProtection.RequirePullRequestReviews.DismissalRestrictions,
                    //PullRequestBypassers = branchProtection.RequirePullRequestReviews.PullRequestBypassers,
                });
            }

            if (branchProtection.RequireStatusChecks != null)
            {
                options.RequiredStatusChecks.Add(new BranchProtectionRequiredStatusCheckArgs()
                {
                    Contexts = branchProtection.RequireStatusChecks.Contexts,
                    Strict = branchProtection.RequireStatusChecks.Strict
                });
            }

            var bp = new GitHub.BranchProtection($"{repoName}-{branchProtection.Pattern}", options);
        }
    }
}