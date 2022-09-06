
using GitHubManagement.Serialization;
using NJsonSchema.Annotations;
using Pulumi;
using Pulumi.Azure.Core;
using Pulumi.AzureAD;
using Pulumi.Github;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace GitHubManagement.Models;

public class TeamOptions
{
    /// <summary>
    /// One or more yaml files that contain a dictionary of string to <see cref="RACRepositoryConfig"/> which is used to manage the repositories owned
    /// by the team identified by <see cref="TeamSlug"/>.
    /// </summary>
    public List<string> RepositoryFiles { get; set; }

    /// <summary>
    /// The team slug which also matches the name of the Azure AD Group
    /// </summary>
    public string TeamSlug { get; set; }
}

public class GlobalContext
{
    //public GitHub.Provider GitHubProvider { get; }

    public AzureAD.Provider AzureADProvider { get; set; }

    public Azure.Provider AzureProvider { get; set; }

    /// <summary>
    /// Gets access to all teams
    /// </summary>
    public Dictionary<string, Team> DataGithubTeams { get; } = new();

    public AzureAD.GetClientConfigResult AdClientConfig { get; set; }

    public ResourceGroup ServicePrincipalsResourceGroup { get; set; }

    public Group ServicePrincipalsGroup { get; set; }

    /// <summary>
    /// Id of the tenant where service principles are created for repos with <see cref="RACRepositoryConfig.create_app_registration"/> enabled.
    /// </summary>
    public string ServicePrincipleTenantID { get; }

    public string GitHubOrganization { get; }

    public Pulumi.Config Config { get; }

    public GlobalContext(Pulumi.Config config)
    {
        Config = config;
        GitHubOrganization = Config.Require(ConfigKeys.GitHubOrganization);
        ServicePrincipleTenantID = Config.Require(ConfigKeys.TenantID);

        Log.Info($"GitHub organization {GitHubOrganization}");

        AzureADProvider = new ("azuread");

        Output<GetGroupResult> grpResult = GetGroup.Invoke(new() 
        { 
            DisplayName = "github-actions-service-principals",
        });

        ServicePrincipalsGroup = Group.Get("github_actions_service_principals", grpResult.Apply(r => r.Id));

        AzureProvider = new("azure-racwa-management", new()
        {
            SubscriptionId = Config.Require(ConfigKeys.ServicePrincipleSubscriptionID),
            SkipProviderRegistration = true
        });

        /*GitHubProvider = new GitHub.Provider("github", new()
        {
            Owner = GitHubOrganization,
            Token = config.RequireSecret(ConfigKeys.GitHubToken)
        })*/;
    }

    public async Task Initialize()
    {
        AdClientConfig = await Pulumi.AzureAD.GetClientConfig.InvokeAsync(new Pulumi.InvokeOptions()
        {
            
        });
    }

    public Team GetGitHubTeam(string slug)
    {
        if (DataGithubTeams.TryGetValue(slug, out var team))
            return team;

        Output<GetTeamResult> result = GetTeam.Invoke(new GetTeamInvokeArgs()
        {
            Slug = slug
        });

        var teamId = result.Apply(r => r.Id);

        team = Team.Get(slug, teamId);

        DataGithubTeams.Add(slug, team);
        return team;
    }
}

#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
[JsonSchemaProcessor(typeof(RepositoryProcessor))]
public class RACRepositoryConfig
{
    /// <seealso cref="RepositoryConfig.Description"/>
    [Description("A description of the repository.")]
    public string description { get; set; }

    /// <seealso cref="RepositoryConfig.Visibility"/>
    [Description("Can be public, private or internal.")]
    public string visibility { get; set; } = "internal";

    /// <seealso cref="RepositoryConfig.HasIssues"/>
    [Description("Set to true to enable the GitHub Issues features on the repository.")]
    public bool has_issues { get; set; }

    /// <seealso cref="RepositoryConfig.HasProjects"/>
    [Description("Set to true to enable the GitHub Projects features on the repository. Per the GitHub documentation when in an " +
        "organization that has disabled repository projects it will default to false and will otherwise default to true. If you " +
        "specify true when it has been disabled it will return an error.")]
    public bool has_projects { get; set; }

    /// <seealso cref="RepositoryConfig.HasWiki"/>
    [Description("Set to true to enable the GitHub Wiki features on the repository.")]
    public bool has_wiki { get; set; }

    /// <seealso cref="RepositoryConfig.IsTemplate"/>
    [Description("Set to true to tell GitHub that this is a template repository.")]
    public bool is_template { get; set; }

    /// <seealso cref="RepositoryConfig.AllowMergeCommit"/>
    [Description("Set to false to disable merge commits on the repository.")]
    public bool allow_merge_commit { get; set; } = true;

    /// <seealso cref="RepositoryConfig.AllowSquashMerge"/>
    [Description("Set to false to disable squash merges on the repository.")]
    public bool allow_squash_merge { get; set; } = true;

    /// <seealso cref="RepositoryConfig.AllowRebaseMerge"/>
    [Description("Set to false to disable rebase merges on the repository.")]
    public bool allow_rebase_merge { get; set; } = true;

    /// <seealso cref="RepositoryConfig.AllowAutoMerge"/>
    [Description("Set to true to allow auto-merging pull requests on the repository.")]
    public bool allow_auto_merge { get; set; }

    /// <seealso cref="RepositoryConfig.DeleteBranchOnMerge"/>
    [Description("Automatically delete head branch after a pull request is merged. Defaults to false.")]
    public bool delete_branch_on_merge { get; set; } = true;

    /// <seealso cref="RepositoryConfig.HasDownloads"/>
    [Description("Set to true to enable the (deprecated) downloads features on the repository.")]
    public bool has_downloads { get; set; }

    /// <seealso cref="RepositoryConfig.AutoInit"/>
    [Description("Set to true to produce an initial commit in the repository.")]
    public bool auto_init { get; set; } = true;

    /// <seealso cref="RepositoryConfig.Archived"/>
    [Description("Specifies if the repository should be archived. Defaults to false. NOTE Currently, the API does not support unarchiving.")]
    public bool archived { get; set; }

    /// <seealso cref="RepositoryConfig.ArchiveOnDestroy"/>
    [Description("Archive the repository instead of deleting on destroy")]
    public bool archive_on_destroy { get; set; }

    /// <seealso cref="RepositoryConfig.VulnerabilityAlerts"/>
    [Description("Set to true to enable security alerts for vulnerable dependencies. Enabling requires alerts to be enabled on the owner" +
        " level. (Note for importing: GitHub enables the alerts on public repos but disables them on private repos by default.) See GitHub Documentation for details")]
    public bool vulnerability_alerts { get; set; }

    /// <seealso cref="RepositoryConfig.DefaultBranch"/>
    [Description("The default branch for the repository. Defaults to main")]
    public string default_branch { get; set; } = "main";

    /// <seealso cref="RepositoryConfig.HomepageUrl"/>
    [Description("URL of a page describing the project.")]
    public string homepage_url { get; set; }

    /// <seealso cref="RepositoryConfig.Topics"/>
    [Description("The list of topics of the repository.")]
    public List<string> topics { get; set; }

    /// <summary>
    /// Dictionary mapping team name to github access
    /// </summary>
    [Description("The permissions of team members regarding the repository. Must be one of pull, triage, push, maintain, admin or the name of an existing or the name of an existing custom repository role")]
    public Dictionary<string, string> teams { get; set; } = new()
    {
        { "default-access", "push"}
    };

    /// <summary>
    /// RACWA specific configuration
    /// </summary>
    [Description("Creates a service principal and storage container for repositories that deploy infrastructure to Azure. Secrets are automatically created and added " + 
        "to the repository. These secrets are also automatically rotated.")]
    public bool create_app_registration { get; set; }

    /// <summary>
    ///  **_Important_** <see cref="disable_default_write"/> property refers to the addition of the _Github Default Access_ team being added to the repo with 
    ///  write access. This is the preferred approach and should only be disabled under special circumstances where write access needs to be more refined. 
    ///  This default Organisation does not allow for repositories to not have the minimum base role of read access to all members of the Organisation
    /// </summary>
    [Description(@"**_Important_** disable_default_write property refers to the addition of the _Github Default Access_ team being added to the repo with " +
        "write access. This is the preferred approach and should only be disabled under special circumstances where write access needs to be more refined. " +
        "This default Organisation does not allow for repositories to not have the minimum base role of read access to all members of the Organisation")]
    public bool disable_default_write { get; set; }

    [Description("When applied, the branch will be protected from forced pushes and deletion. Additional constraints, such as required status checks " 
        + "or restrictions on users, teams, and apps, can also be configured.")]
    public List<BranchProtection> branch_protection { get; set; }

    [Description]
    public RepositoryCodeowner Codeowner { get; set; }

    /// <summary>
    /// Modifies any team derived default properties on this instance 
    /// </summary>
    public void ApplyDefaults(TeamOptions teamOptions)
    {
        // Always add team slug as a topic if it doesnt exist
        topics = (topics ?? Enumerable.Empty<string>()).Concat(new[] { teamOptions.TeamSlug }).Distinct().ToList();
    }
}

public class BranchProtection
{
    [Description("Setting this to true to allow the branch to be deleted.")]
    public bool? AllowsDeletions { get; set; }

    [Description("Setting this to true to allow force pushes on the branch.")]
    public bool? AllowsForcePushes { get; set; }

    [Description("Setting this to true to block creating the branch.")]
    public bool? BlocksCreations { get; set; }

    [Description("Setting this to true enforces status checks for repository administrators.")]
    public bool? EnforceAdmins { get; set; }

    [Description("Identifies the protection rule pattern. e.g. develop")]
    [Required]
    public string Pattern { get; set; }

    [Description("The list of actor IDs that may push to the branch.")]
    public string PushRestriction {get;set;}

    [Description("Setting this to true requires all conversations on code must be resolved before a pull request can be merged.")]
    public bool? RequireConversationResolution {get;set;}

    [Description("Setting this to true requires all commits to be signed with GPG.")]
    public bool? RequireSignedCommits { get; set; }

    [Description("Setting this to true enforces a linear commit Git history, which prevents anyone from pushing merge commits to a branch. Note this may prevent git migration merging.")]
    public bool? RequiredLinearHistory { get; set; }

    [Description("Enforce restrictions for pull request reviews.")]
    public BranchProtectionRequiredPullRequestReview RequirePullRequestReviews { get; set; }

    /// <summary>
    /// Note that the naming of this property is mismatched to the 
    /// </summary>
    [Description("Enforce restrictions for required status checks.")]
    public BranchProtectionRequiredStatusCheck RequireStatusChecks { get; set; }
}

public class BranchProtectionRequiredPullRequestReview
{
    
    [Description("Dismiss approved reviews automatically when a new commit is pushed. Defaults to false.")]
    public bool? DismissStaleReviews { get; set; }

    [Description("Restrict pull request review dismissals.")]
    public bool? RestrictDismissals { get; set; }

    //[Description("Restrict pull request review dismissals.")]
    //public string DismissalRestrictions { get; set; }
    
    //[Description("The list of actor IDs that are allowed to bypass pull request requirements.")]
    //public string PullRequestBypassers { get; set; }
    
    [Description("Require an approved review in pull requests including files with a designated code owner. Defaults to false.")]
    public bool? RequireCodeOwnerReviews { get; set; }
    
    [Description("Require x number of approvals to satisfy branch protection requirements. If this is specified it must be a number between 0-6.")]
    public int? RequiredApprovingReviewCount { get; set; }
}

public class BranchProtectionRequiredStatusCheck
{
    [Description("(Optional) The list of status checks to require in order to merge into this branch. No status checks are required by default.")]
    public string[] Contexts { get; set; }

    [Description("(Optional) Require branches to be up to date before merging")]
    public bool? Strict { get; set; }
}

public class RepositoryCodeowner
{
    [Description("Whether the GitHub CODEOWNERS file is automatically created.")]
    public bool Create { get; set; } = true;

    [Description("List of code owners")]
    public List<CodeownerApprover> Approvers { get; set; }    
}

public class CodeownerApprover
{
    [Description("")]
    public double? Priority { get; set; }

    [Description("")]
    public string Approver { get; set; }

    [Description("File globbing pattern to match the approver with a set of files")]
    public string Pattern { get; set; }

    [Description("")]
    public string Comment { get; set; }
}


#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
