using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Webhooks;
using Octokit.Webhooks.Events;
using Octokit.Webhooks.Events.IssueComment;
using Octokit.Webhooks.Events.Issues;
using Octokit.Webhooks.Models;
using System.Text.RegularExpressions;

namespace Starward_Bot;

internal class IssueEventProcessor : WebhookEventProcessor
{

    private readonly ILogger<IssueEventProcessor> _logger;

    private readonly IMemoryCache _memory;

    private GitHubClient _client;


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    public IssueEventProcessor(ILogger<IssueEventProcessor> logger, IMemoryCache memory)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑声明为可以为 null。
    {
        _logger = logger;
        _memory = memory;
    }



    private async Task EnsureAppClient()
    {
        if (_memory.TryGetValue("GithubClient", out GitHubClient? client))
        {
            _client = client!;
            return;
        }
        client = await GithubUtil.CreateGithubClient();
        _memory.Set("GithubClient", client, DateTimeOffset.Now + TimeSpan.FromSeconds(10));
        _client = client;
    }




    protected override async Task ProcessIssuesWebhookAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        try
        {
            if (issuesEvent.Repository is { FullName: "Scighost/Starward" })
            {
                await EnsureAppClient();

                if (action == IssuesAction.Opened || action == IssuesAction.Reopened)
                {
                    await OnIssueOpenedAsync(headers, issuesEvent, action);
                }

                if (action == IssuesAction.Closed)
                {
                    await OnIssueClosedAsync(headers, issuesEvent, action);
                }

                if (action == IssuesAction.Labeled)
                {
                    await OnIssueLabeledAsync(headers, issuesEvent, action);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Handle issue event");
            throw;
        }
    }




    private async Task OnIssueOpenedAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        bool close = false;
        string title = issuesEvent.Issue.Title;
        string? body = issuesEvent.Issue.Body;

        if (issuesEvent.Issue.Labels.Any(x => x.Name == "keep open"))
        {
            return;
        }

        if (!close && string.IsNullOrWhiteSpace(title))
        {
            close = true;
        }
        if (!close && string.IsNullOrWhiteSpace(body))
        {
            close = true;
        }
        if (!close && string.IsNullOrWhiteSpace(Regex.Match(title, @"(\[[^\]]*\])?\s*(.*)").Groups[2].Value))
        {
            close = true;
        }

        if (close)
        {
            var issueUpdate = new IssueUpdate { Title = title };
            issueUpdate.AddLabel("invalid");
            issueUpdate.State = ItemState.Closed;
            issueUpdate.StateReason = ItemStateReason.NotPlanned;
            await _client.Issue.Comment.Create(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, "This issue will be closed for no title or content.");
            await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
        }
    }



    private async Task OnIssueClosedAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        if (issuesEvent.Issue.Labels.Any(x => x.Name == "triage"))
        {
            var issue = await _client.Issue.Get("Scighost", "Starward", (int)issuesEvent.Issue.Number);
            var issueUpdate = issue.ToUpdate();
            issueUpdate.RemoveLabel("triage");
            await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
        }
    }



    private async Task OnIssueLabeledAsync(WebhookHeaders headers, IssuesEvent issuesEvent, IssuesAction action)
    {
        if (issuesEvent.Sender is { Login: "Scighost" })
        {
            if (issuesEvent.Issue.State?.Value is IssueState.Closed)
            {
                return;
            }
            else if (issuesEvent.Issue.Labels.Any(x => x.Name is "invalid"))
            {
                var issue = await _client.Issue.Get("Scighost", "Starward", (int)issuesEvent.Issue.Number);
                var issueUpdate = issue.ToUpdate();
                issueUpdate.State = ItemState.Closed;
                issueUpdate.StateReason = ItemStateReason.NotPlanned;
                await _client.Issue.Comment.Create("Scighost", "Starward", (int)issuesEvent.Issue.Number, "This issue will be closed for something invalid.\n由于存在无效内容，该 issue 将被关闭。");
                await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
            }
            else if (issuesEvent.Issue.Labels.Any(x => x.Name is "duplicate"))
            {
                var issue = await _client.Issue.Get("Scighost", "Starward", (int)issuesEvent.Issue.Number);
                var issueUpdate = issue.ToUpdate();
                issueUpdate.State = ItemState.Closed;
                issueUpdate.StateReason = ItemStateReason.NotPlanned;
                await _client.Issue.Comment.Create("Scighost", "Starward", (int)issuesEvent.Issue.Number, "This issue will be closed for duplicate.\n重复的 issue 将被关闭。");
                await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
            }
            else if (issuesEvent.Issue.Labels.Any(x => x.Name is "need more info"))
            {
                var issue = await _client.Issue.Get("Scighost", "Starward", (int)issuesEvent.Issue.Number);
                var issueUpdate = issue.ToUpdate();
                issueUpdate.State = ItemState.Closed;
                issueUpdate.StateReason = ItemStateReason.NotPlanned;
                await _client.Issue.Comment.Create("Scighost", "Starward", (int)issuesEvent.Issue.Number, "Sorry, based on the information you provided, the developer is unable to resolve this issue.\n很抱歉，根据您提供的信息，开发者无法解决此问题。");
                await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
            }
            else if (issuesEvent.Issue.Labels.Any(x => x.Name is "do not understand"))
            {
                var issue = await _client.Issue.Get("Scighost", "Starward", (int)issuesEvent.Issue.Number);
                var issueUpdate = issue.ToUpdate();
                issueUpdate.State = ItemState.Closed;
                issueUpdate.StateReason = ItemStateReason.NotPlanned;
                await _client.Issue.Comment.Create("Scighost", "Starward", (int)issuesEvent.Issue.Number, "Thank you for your feedback or suggestions, but we're sorry that the developer couldn't understand what you posted.\n感谢您的反馈或建议，但是很抱歉，开发者无法理解您发布的内容。");
                await _client.Issue.Update(issuesEvent.Repository.Id, (int)issuesEvent.Issue.Number, issueUpdate);
            }
        }
    }




    protected override async Task ProcessIssueCommentWebhookAsync(WebhookHeaders headers, IssueCommentEvent issueCommentEvent, IssueCommentAction action)
    {
        await EnsureAppClient();
        if (action == IssueCommentAction.Deleted && issueCommentEvent.Repository is { FullName: "Scighost/Starward" })
        {
            if (issueCommentEvent.Sender?.Login is "Scighost")
            {
                return;
            }
            string body = $"""
                > @{issueCommentEvent.Comment.User.Login} deleted the following comment
                
                {issueCommentEvent.Comment.Body}
                """;
            await _client.Issue.Comment.Create("Scighost", "Starward", (int)issueCommentEvent.Issue.Number, body);
        }
    }




}
