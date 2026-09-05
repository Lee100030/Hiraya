using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class NotificationSendRequest
{
    public string Audience { get; set; } = NotificationAudiences.Admins;
    public string UserId { get; set; } = "";
    public string Kind { get; set; } = NotificationKinds.System;
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Href { get; set; } = "";
}

public class NewsSaveRequest
{
    public string NewsId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Publish { get; set; } = true;
    public bool Notify { get; set; } = true;
}

public class NotificationService
{
    private readonly IHirayaRepository _repository;

    public NotificationService(IHirayaRepository repository) => _repository = repository;

    public static IEnumerable<SystemAlert> Inbox(HirayaDatabase db, HirayaUser user, bool includeArchived = false)
    {
        return db.Alerts
            .Where(a => a.RecipientId == user.UserId)
            .Where(a => includeArchived || !IsArchived(a))
            .OrderByDescending(a => a.CreatedAt);
    }

    public static int UnreadCount(HirayaDatabase db, HirayaUser user) =>
        Inbox(db, user).Count(a => !a.Read);

    public static IEnumerable<NewsItem> PublishedNews(HirayaDatabase db) =>
        db.News
            .Where(n => n.IsPublished && n.Status != "archived")
            .OrderByDescending(n => n.PublishedAt);

    public static void Push(
        HirayaDatabase db,
        string recipientId,
        string message,
        string kind = NotificationKinds.System,
        string? title = null,
        string? href = null)
    {
        if (string.IsNullOrWhiteSpace(recipientId) || string.IsNullOrWhiteSpace(message))
            return;

        db.Alerts.Add(new SystemAlert
        {
            AlertId = IdFactory.New("al"),
            Kind = string.IsNullOrWhiteSpace(kind) ? NotificationKinds.System : kind,
            Title = string.IsNullOrWhiteSpace(title) ? NotificationKinds.Label(kind) : title.Trim(),
            Message = message.Trim(),
            RecipientId = recipientId,
            Href = href?.Trim() ?? "",
            Status = "active",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Read = false
        });
    }

    public static void PushToUsers(
        HirayaDatabase db,
        IEnumerable<string> recipientIds,
        string message,
        string kind = NotificationKinds.System,
        string? title = null,
        string? href = null)
    {
        foreach (var id in recipientIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            Push(db, id, message, kind, title, href);
    }

    public static IReadOnlyList<HirayaUser> Recipients(HirayaDatabase db, string audience, string? userId = null)
    {
        var active = db.Users.Where(u =>
            !string.Equals(u.Status, "archived", StringComparison.OrdinalIgnoreCase));

        return audience switch
        {
            NotificationAudiences.User when !string.IsNullOrWhiteSpace(userId) =>
                active.Where(u => u.UserId == userId).ToList(),
            NotificationAudiences.Admins =>
                active.Where(u => u.Role is UserRoles.SuperAdmin or UserRoles.Admin).ToList(),
            NotificationAudiences.Teachers =>
                active.Where(u => u.Role == UserRoles.Teacher).ToList(),
            NotificationAudiences.Staff =>
                active.Where(u => u.Role == UserRoles.Staff).ToList(),
            NotificationAudiences.Parents =>
                active.Where(u => u.Role == UserRoles.Parent).ToList(),
            NotificationAudiences.Employees =>
                active.Where(u => u.Role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Teacher or UserRoles.Staff).ToList(),
            NotificationAudiences.Everyone =>
                active.ToList(),
            _ => []
        };
    }

    public static bool IsArchived(SystemAlert alert) =>
        string.Equals(alert.Status, "archived", StringComparison.OrdinalIgnoreCase);

    public async Task SendAsync(NotificationSendRequest input, HirayaUser actor)
    {
        if (!Navigation.CanSendNotifications(actor.Role))
            throw new InvalidOperationException("You cannot send notifications.");
        if (string.IsNullOrWhiteSpace(input.Message))
            throw new InvalidOperationException("Message is required.");
        if (input.Audience == NotificationAudiences.User && string.IsNullOrWhiteSpace(input.UserId))
            throw new InvalidOperationException("Select a recipient.");

        var title = string.IsNullOrWhiteSpace(input.Title) ? NotificationKinds.Label(input.Kind) : input.Title.Trim();
        var kind = NotificationKinds.All.Contains(input.Kind) ? input.Kind : NotificationKinds.System;
        var href = input.Href.Trim();

        await _repository.MutateAsync(async db =>
        {
            var people = Recipients(db, input.Audience, input.UserId);
            if (people.Count == 0)
                throw new InvalidOperationException("No recipients matched that audience.");

            PushToUsers(db, people.Select(u => u.UserId), input.Message.Trim(), kind, title, href);
            AuditService.Append(db, actor, AuditModules.Notifications, "send",
                "notification", input.Audience, $"Sent “{title}” to {people.Count} recipient(s).");
            await Task.CompletedTask;
        });
    }

    public async Task MarkReadAsync(string alertId, HirayaUser actor, bool read = true)
    {
        await _repository.MutateAsync(async db =>
        {
            var alert = db.Alerts.FirstOrDefault(a => a.AlertId == alertId && a.RecipientId == actor.UserId)
                        ?? throw new InvalidOperationException("Notification not found.");
            alert.Read = read;
            await Task.CompletedTask;
        });
    }

    public async Task MarkAllReadAsync(HirayaUser actor)
    {
        await _repository.MutateAsync(async db =>
        {
            foreach (var alert in Inbox(db, actor).Where(a => !a.Read))
                alert.Read = true;
            await Task.CompletedTask;
        });
    }

    public async Task ArchiveAsync(string alertId, HirayaUser actor)
    {
        await _repository.MutateAsync(async db =>
        {
            var alert = db.Alerts.FirstOrDefault(a => a.AlertId == alertId && a.RecipientId == actor.UserId)
                        ?? throw new InvalidOperationException("Notification not found.");
            alert.Status = "archived";
            alert.Read = true;
            await Task.CompletedTask;
        });
    }

    public async Task SaveNewsAsync(NewsSaveRequest input, HirayaUser actor)
    {
        if (!Navigation.CanManageAnnouncements(actor.Role))
            throw new InvalidOperationException("You cannot manage announcements.");
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Body))
            throw new InvalidOperationException("Title and body are required.");

        await _repository.MutateAsync(async db =>
        {
            var isNew = string.IsNullOrWhiteSpace(input.NewsId);
            var id = isNew ? IdFactory.New("news") : input.NewsId;
            var existing = db.News.FirstOrDefault(n => n.NewsId == id);
            if (!isNew && existing == null)
                throw new InvalidOperationException("Announcement not found.");
            if (existing != null && existing.Status == "archived")
                throw new InvalidOperationException("Archived announcements cannot be edited.");

            var wasPublished = existing?.IsPublished == true;
            var now = DateTime.UtcNow.ToString("o");
            var item = new NewsItem
            {
                NewsId = id,
                Title = input.Title.Trim(),
                Body = input.Body.Trim(),
                CreatedBy = existing?.CreatedBy is { Length: > 0 } by ? by : actor.UserId,
                IsPublished = input.Publish,
                Status = input.Publish ? "published" : "draft",
                PublishedAt = input.Publish
                    ? (wasPublished ? existing!.PublishedAt : now)
                    : existing?.PublishedAt ?? ""
            };

            db.News.RemoveAll(n => n.NewsId == id);
            db.News.Add(item);

            if (input.Publish && input.Notify && (!wasPublished || isNew))
            {
                PushToUsers(
                    db,
                    Recipients(db, NotificationAudiences.Everyone).Select(u => u.UserId),
                    item.Body,
                    NotificationKinds.Announcement,
                    item.Title,
                    "announcements");
            }

            AuditService.Append(db, actor, AuditModules.Announcements, isNew ? "create" : "update",
                "news", id, $"{(input.Publish ? "Published" : "Saved draft")} “{item.Title}”.");
            await Task.CompletedTask;
        });
    }

    public async Task ArchiveNewsAsync(string newsId, HirayaUser actor)
    {
        if (!Navigation.CanManageAnnouncements(actor.Role))
            throw new InvalidOperationException("You cannot manage announcements.");

        await _repository.MutateAsync(async db =>
        {
            var item = db.News.FirstOrDefault(n => n.NewsId == newsId)
                       ?? throw new InvalidOperationException("Announcement not found.");
            item.Status = "archived";
            item.IsPublished = false;
            AuditService.Append(db, actor, AuditModules.Announcements, "archive", "news", newsId, $"Archived “{item.Title}”.");
            await Task.CompletedTask;
        });
    }
}
