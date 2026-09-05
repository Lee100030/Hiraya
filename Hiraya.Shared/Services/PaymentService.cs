using Hiraya.Shared.Models;
using Hiraya.Shared.Services.Firebase;

namespace Hiraya.Shared.Services;

public class PaymentSaveRequest
{
    public string PaymentId { get; set; } = "";
    public string EnrollmentId { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string DueAt { get; set; } = "";
    public string ReferenceNumber { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class PaymentCollectRequest
{
    public string PaymentId { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;
    public string ReferenceNumber { get; set; } = "";
    public string Notes { get; set; } = "";
}

public class PaymentService
{
    private readonly IHirayaRepository _repository;

    public PaymentService(IHirayaRepository repository) => _repository = repository;

    public static IEnumerable<Payment> Visible(HirayaDatabase db, HirayaUser user)
    {
        IEnumerable<Payment> payments = db.Payments.Where(p => p.PaymentStatus != PaymentStatuses.Cancelled
                                                               || Navigation.CanManagePayments(user.Role));

        if (user.Role is UserRoles.SuperAdmin or UserRoles.Admin or UserRoles.Staff)
            return payments;

        if (user.Role == UserRoles.Teacher)
        {
            var roster = db.Enrollments
                .Where(e => e.TeacherId == user.UserId ||
                            db.Classes.Any(c => c.ClassId == e.ClassId && c.TeacherId == user.UserId))
                .Select(e => e.StudentId)
                .ToHashSet();
            return payments.Where(p => roster.Contains(ResolveStudentId(db, p)));
        }

        if (user.Role is UserRoles.Parent or UserRoles.Student)
        {
            var kids = FamilyPortal.StudentIds(db, user);
            return payments.Where(p =>
                p.PaymentStatus != PaymentStatuses.Cancelled &&
                kids.Contains(ResolveStudentId(db, p)));
        }

        return Array.Empty<Payment>();
    }

    public static string ResolveStudentId(HirayaDatabase db, Payment payment)
    {
        if (!string.IsNullOrWhiteSpace(payment.StudentId))
            return payment.StudentId;
        return db.Enrollments.FirstOrDefault(e => e.EnrollmentId == payment.EnrollmentId)?.StudentId ?? "";
    }

    public static DateTime? DueLocal(Payment payment)
    {
        var raw = string.IsNullOrWhiteSpace(payment.DueAt) ? payment.PaymentDate : payment.DueAt;
        if (!DateTime.TryParse(raw, out var due))
            return null;
        return due.Kind == DateTimeKind.Utc ? due.ToLocalTime() : due;
    }

    public static string EffectiveStatus(Payment payment)
    {
        if (!PaymentStatuses.IsOpen(payment.PaymentStatus) && payment.PaymentStatus != PaymentStatuses.Overdue)
            return string.IsNullOrWhiteSpace(payment.PaymentStatus) ? PaymentStatuses.Pending : payment.PaymentStatus;

        var remaining = Remaining(payment);
        if (remaining <= 0 && payment.AmountPaid > 0)
            return PaymentStatuses.Paid;

        var due = DueLocal(payment);
        if (due is { } day && day.Date < DateTime.Today && remaining > 0)
            return PaymentStatuses.Overdue;

        if (payment.AmountPaid > 0 && remaining > 0)
            return PaymentStatuses.Partial;

        return PaymentStatuses.Pending;
    }

    public static decimal Remaining(Payment payment)
    {
        if (payment.PaymentStatus is PaymentStatuses.Paid or PaymentStatuses.Refunded or PaymentStatuses.Cancelled)
            return 0;
        return Math.Max(0, payment.Amount - payment.AmountPaid);
    }

    public static bool IsVisible(HirayaDatabase db, HirayaUser user, Payment payment) =>
        Visible(db, user).Any(p => p.PaymentId == payment.PaymentId);

    public async Task SaveChargeAsync(PaymentSaveRequest input, HirayaUser actor)
    {
        if (!Navigation.CanManagePayments(actor.Role))
            throw new InvalidOperationException("You cannot create or edit charges.");
        if (string.IsNullOrWhiteSpace(input.EnrollmentId))
            throw new InvalidOperationException("Select an enrollment.");
        if (input.Amount <= 0)
            throw new InvalidOperationException("Amount must be greater than zero.");
        if (!PaymentMethods.All.Contains(input.PaymentMethod))
            throw new InvalidOperationException("Select a payment method.");
        if (!DateTime.TryParse(input.DueAt, out var due))
            throw new InvalidOperationException("Due date is required.");

        await _repository.MutateAsync(async db =>
        {
            var enrollment = db.Enrollments.FirstOrDefault(e => e.EnrollmentId == input.EnrollmentId)
                             ?? throw new InvalidOperationException("Enrollment not found.");
            var student = db.Students.FirstOrDefault(s => s.StudentId == enrollment.StudentId)
                          ?? throw new InvalidOperationException("Student for that enrollment was not found.");

            var now = DateTime.UtcNow.ToString("o");
            var dueIso = DateTime.SpecifyKind(due.Date, DateTimeKind.Local).ToUniversalTime().ToString("o");
            var isNew = string.IsNullOrWhiteSpace(input.PaymentId);
            var id = isNew ? IdFactory.New("pay") : input.PaymentId;
            var existing = db.Payments.FirstOrDefault(p => p.PaymentId == id);

            if (!isNew && existing == null)
                throw new InvalidOperationException("Payment not found.");
            if (existing != null && existing.PaymentStatus is PaymentStatuses.Paid or PaymentStatuses.Refunded)
                throw new InvalidOperationException("Paid or refunded charges cannot be rewritten. Record a new charge instead.");
            if (existing != null && existing.PaymentStatus == PaymentStatuses.Cancelled)
                throw new InvalidOperationException("Cancelled charges cannot be edited.");

            var next = new Payment
            {
                PaymentId = id,
                EnrollmentId = enrollment.EnrollmentId,
                StudentId = student.StudentId,
                Amount = decimal.Round(input.Amount, 2),
                AmountPaid = existing?.AmountPaid ?? 0,
                PaymentMethod = input.PaymentMethod,
                ReferenceNumber = input.ReferenceNumber.Trim(),
                Notes = input.Notes.Trim(),
                DueAt = dueIso,
                PaymentDate = dueIso,
                PaidAt = existing?.PaidAt ?? "",
                RecordedBy = actor.UserId,
                History = (existing?.History ?? []).ToList()
            };
            next.PaymentStatus = EffectiveStatus(next);
            next.History.Add(new PaymentHistoryEntry
            {
                EntryId = IdFactory.New("pyh"),
                ChangedBy = actor.UserId,
                ChangedAt = now,
                Action = isNew ? "created" : "updated",
                Summary = isNew
                    ? $"Charge ₱{next.Amount:N2} due {due:yyyy-MM-dd}."
                    : $"Charge updated. Amount ₱{next.Amount:N2}, due {due:yyyy-MM-dd}."
            });

            db.Payments.RemoveAll(p => p.PaymentId == id);
            db.Payments.Add(next);

            if (isNew && !string.IsNullOrWhiteSpace(student.ParentId))
            {
                NotificationService.Push(
                    db,
                    student.ParentId,
                    $"New ₱{next.Amount:N2} charge for {StudentNames.Display(student)} is due {due:MMM d, yyyy}.",
                    NotificationKinds.Payment,
                    "New charge",
                    "payments");
            }

            AuditService.Append(db, actor, AuditModules.Payments, isNew ? "charge" : "update",
                "payment", id, $"{(isNew ? "Created" : "Updated")} ₱{next.Amount:N2} charge for {StudentNames.Display(student)}.");
            await Task.CompletedTask;
        });
    }

    public async Task CollectAsync(PaymentCollectRequest input, HirayaUser actor)
    {
        if (!Navigation.CanManagePayments(actor.Role))
            throw new InvalidOperationException("You cannot record collections.");
        if (input.Amount <= 0)
            throw new InvalidOperationException("Collection amount must be greater than zero.");
        if (!PaymentMethods.All.Contains(input.PaymentMethod))
            throw new InvalidOperationException("Select a payment method.");

        await _repository.MutateAsync(async db =>
        {
            var payment = db.Payments.FirstOrDefault(p => p.PaymentId == input.PaymentId)
                          ?? throw new InvalidOperationException("Payment not found.");
            if (payment.PaymentStatus is PaymentStatuses.Cancelled or PaymentStatuses.Refunded)
                throw new InvalidOperationException("This charge cannot accept payments.");

            var remaining = Remaining(payment);
            if (remaining <= 0)
                throw new InvalidOperationException("This charge is already paid in full.");
            if (input.Amount - remaining > 0.009m)
                throw new InvalidOperationException($"Collection cannot exceed the remaining ₱{remaining:N2}.");

            var now = DateTime.UtcNow.ToString("o");
            payment.AmountPaid = decimal.Round(payment.AmountPaid + input.Amount, 2);
            payment.PaymentMethod = input.PaymentMethod;
            if (!string.IsNullOrWhiteSpace(input.ReferenceNumber))
                payment.ReferenceNumber = input.ReferenceNumber.Trim();
            if (!string.IsNullOrWhiteSpace(input.Notes))
                payment.Notes = input.Notes.Trim();
            payment.RecordedBy = actor.UserId;
            payment.PaymentStatus = EffectiveStatus(payment);
            if (payment.PaymentStatus == PaymentStatuses.Paid)
            {
                payment.PaidAt = now;
                payment.PaymentDate = now;
            }

            payment.History ??= [];
            payment.History.Add(new PaymentHistoryEntry
            {
                EntryId = IdFactory.New("pyh"),
                ChangedBy = actor.UserId,
                ChangedAt = now,
                Action = "collected",
                Summary = $"Collected ₱{input.Amount:N2} via {PaymentMethods.Label(input.PaymentMethod)}. Remaining ₱{Remaining(payment):N2}."
            });

            var student = db.Students.FirstOrDefault(s => s.StudentId == ResolveStudentId(db, payment));
            if (student != null && !string.IsNullOrWhiteSpace(student.ParentId))
            {
                NotificationService.Push(
                    db,
                    student.ParentId,
                    payment.PaymentStatus == PaymentStatuses.Paid
                        ? $"Payment of ₱{payment.Amount:N2} for {StudentNames.Display(student)} is complete."
                        : $"Partial payment of ₱{input.Amount:N2} recorded for {StudentNames.Display(student)}. Remaining ₱{PaymentService.Remaining(payment):N2}.",
                    NotificationKinds.Payment,
                    payment.PaymentStatus == PaymentStatuses.Paid ? "Payment received" : "Partial payment",
                    "payments");
            }

            AuditService.Append(db, actor, AuditModules.Payments, "collect",
                "payment", payment.PaymentId,
                $"Collected ₱{input.Amount:N2} ({payment.PaymentStatus}).");
            await Task.CompletedTask;
        });
    }

    public async Task MarkPaidAsync(string paymentId, HirayaUser actor)
    {
        var db = await _repository.LoadAsync();
        var live = db.Payments.FirstOrDefault(p => p.PaymentId == paymentId)
                   ?? throw new InvalidOperationException("Payment not found.");
        var remaining = Remaining(live);
        if (remaining <= 0)
            throw new InvalidOperationException("This charge is already paid in full.");

        await CollectAsync(new PaymentCollectRequest
        {
            PaymentId = paymentId,
            Amount = remaining,
            PaymentMethod = string.IsNullOrWhiteSpace(live.PaymentMethod) ? PaymentMethods.Cash : live.PaymentMethod,
            ReferenceNumber = live.ReferenceNumber,
            Notes = "Marked paid in full."
        }, actor);
    }

    public async Task CancelAsync(string paymentId, HirayaUser actor)
    {
        if (!Navigation.CanManagePayments(actor.Role))
            throw new InvalidOperationException("You cannot cancel charges.");

        await _repository.MutateAsync(async db =>
        {
            var payment = db.Payments.FirstOrDefault(p => p.PaymentId == paymentId)
                          ?? throw new InvalidOperationException("Payment not found.");
            if (payment.AmountPaid > 0)
                throw new InvalidOperationException("Refund collected amounts before cancelling, or leave the charge as paid.");
            if (payment.PaymentStatus == PaymentStatuses.Cancelled)
                return;

            payment.PaymentStatus = PaymentStatuses.Cancelled;
            payment.RecordedBy = actor.UserId;
            payment.History ??= [];
            payment.History.Add(new PaymentHistoryEntry
            {
                EntryId = IdFactory.New("pyh"),
                ChangedBy = actor.UserId,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Action = "cancelled",
                Summary = "Charge cancelled. No collection was recorded."
            });
            AuditService.Append(db, actor, AuditModules.Payments, "cancel",
                "payment", paymentId, "Cancelled an unpaid charge.");
            await Task.CompletedTask;
        });
    }

    public async Task RefundAsync(string paymentId, HirayaUser actor)
    {
        if (!Navigation.CanManagePayments(actor.Role))
            throw new InvalidOperationException("You cannot refund charges.");

        await _repository.MutateAsync(async db =>
        {
            var payment = db.Payments.FirstOrDefault(p => p.PaymentId == paymentId)
                          ?? throw new InvalidOperationException("Payment not found.");
            if (payment.AmountPaid <= 0)
                throw new InvalidOperationException("Nothing has been collected to refund.");
            if (payment.PaymentStatus == PaymentStatuses.Refunded)
                return;

            var refunded = payment.AmountPaid;
            payment.PaymentStatus = PaymentStatuses.Refunded;
            payment.RecordedBy = actor.UserId;
            payment.History ??= [];
            payment.History.Add(new PaymentHistoryEntry
            {
                EntryId = IdFactory.New("pyh"),
                ChangedBy = actor.UserId,
                ChangedAt = DateTime.UtcNow.ToString("o"),
                Action = "refunded",
                Summary = $"Refunded ₱{refunded:N2}."
            });

            var student = db.Students.FirstOrDefault(s => s.StudentId == ResolveStudentId(db, payment));
            if (student != null && !string.IsNullOrWhiteSpace(student.ParentId))
            {
                NotificationService.Push(
                    db,
                    student.ParentId,
                    $"A ₱{refunded:N2} refund was recorded for {StudentNames.Display(student)}.",
                    NotificationKinds.Payment,
                    "Refund recorded",
                    "payments");
            }

            AuditService.Append(db, actor, AuditModules.Payments, "refund",
                "payment", paymentId, $"Refunded ₱{refunded:N2}.");
            await Task.CompletedTask;
        });
    }
}
