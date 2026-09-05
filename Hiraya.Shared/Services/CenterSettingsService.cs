using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class CenterSettingsService
{
    private readonly IHirayaRepository _repository;

    public CenterSettingsService(IHirayaRepository repository) => _repository = repository;

    public async Task SaveAsync(CenterSettings input, HirayaUser actor)
    {
        if (!Navigation.CanManageSystemSettings(actor.Role))
            throw new InvalidOperationException("You cannot change system settings.");
        if (string.IsNullOrWhiteSpace(input.CenterName))
            throw new InvalidOperationException("Learning center name is required.");

        await _repository.MutateAsync(async db =>
        {
            db.Settings = new CenterSettings
            {
                SettingsId = string.IsNullOrWhiteSpace(input.SettingsId) ? "center" : input.SettingsId,
                CenterName = input.CenterName.Trim(),
                LogoPath = input.LogoPath.Trim(),
                Address = input.Address.Trim(),
                Phone = input.Phone.Trim(),
                Email = input.Email.Trim(),
                Website = input.Website.Trim(),
                Description = input.Description.Trim(),
                TimeZone = string.IsNullOrWhiteSpace(input.TimeZone) ? "Asia/Manila" : input.TimeZone.Trim(),
                DefaultPeriod = input.DefaultPeriod.Trim(),
                BusinessHours = input.BusinessHours.Trim(),
                DefaultAttendanceStatus = string.IsNullOrWhiteSpace(input.DefaultAttendanceStatus)
                    ? AttendanceStatuses.Present
                    : input.DefaultAttendanceStatus.Trim(),
                DefaultPaymentMethod = string.IsNullOrWhiteSpace(input.DefaultPaymentMethod)
                    ? PaymentMethods.Cash
                    : input.DefaultPaymentMethod.Trim(),
                NotificationsEnabled = input.NotificationsEnabled,
                NotifyAttendance = input.NotifyAttendance,
                NotifyPayments = input.NotifyPayments,
                NotifySchedule = input.NotifySchedule,
                NotifyProgress = input.NotifyProgress,
                ModuleAllowedExtensions = input.ModuleAllowedExtensions.Trim(),
                ModuleMaxFileMb = input.ModuleMaxFileMb,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
            AuditService.Append(db, actor, AuditModules.Settings, "update", "system", "settings",
                $"{actor.Fullname} updated system settings.");
            await Task.CompletedTask;
        });
    }
}
