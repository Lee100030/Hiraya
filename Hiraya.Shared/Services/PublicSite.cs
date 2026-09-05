using Hiraya.Shared.Models;

namespace Hiraya.Shared.Services;

public sealed class PublicProgramDto
{
    public string ProgramId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class PublicAnnouncementDto
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string PublishedAt { get; set; } = "";
}

public sealed class PublicSiteDto
{
    public string CenterName { get; set; } = "HIRAYA Learning Center";
    public string Description { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Website { get; set; } = "";
    public string BusinessHours { get; set; } = "";
    public List<PublicProgramDto> Programs { get; set; } = [];
    public List<PublicAnnouncementDto> Announcements { get; set; } = [];
}

public sealed class PublicEnrollmentRequest
{
    public string StudentFullname { get; set; } = "";
    public string StudentBirthdate { get; set; } = "";
    public string StudentGender { get; set; } = "";
    public string PreferredProgram { get; set; } = "";
    public string ParentFullname { get; set; } = "";
    public string ParentEmail { get; set; } = "";
    public string ParentPhone { get; set; } = "";
}

public sealed class PublicEnrollmentResult
{
    public string ApplicationId { get; set; } = "";
}

public static class PublicSiteCatalog
{
    public static PublicSiteDto From(HirayaDatabase db)
    {
        var settings = db.Settings;
        var description = string.IsNullOrWhiteSpace(settings.Description)
            ? "Hiraya Learning Center supports early learning, guided play, and family partnership in a safe, welcoming environment."
            : settings.Description;

        return new PublicSiteDto
        {
            CenterName = string.IsNullOrWhiteSpace(settings.CenterName)
                ? "HIRAYA Learning Center"
                : settings.CenterName,
            Description = description,
            Address = settings.Address,
            Phone = settings.Phone,
            Email = settings.Email,
            Website = settings.Website,
            BusinessHours = string.IsNullOrWhiteSpace(settings.BusinessHours)
                ? "Monday–Friday, 8:00–17:00"
                : settings.BusinessHours,
            Programs = db.Programs
                .Where(p => !string.Equals(p.Status, "archived", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.Name)
                .Select(p => new PublicProgramDto
                {
                    ProgramId = p.ProgramId,
                    Name = p.Name,
                    Description = string.IsNullOrWhiteSpace(p.Description)
                        ? "Ask the center for details about this program."
                        : p.Description
                })
                .ToList(),
            Announcements = NotificationService.PublishedNews(db)
                .Take(5)
                .Select(n => new PublicAnnouncementDto
                {
                    Title = n.Title,
                    Body = n.Body,
                    PublishedAt = n.PublishedAt
                })
                .ToList()
        };
    }
}
