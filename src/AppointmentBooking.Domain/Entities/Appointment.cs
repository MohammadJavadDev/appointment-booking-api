using AppointmentBooking.Domain.Common;

namespace AppointmentBooking.Domain.Entities;

public enum AppointmentStatus
{
    Booked = 0,
    Cancelled = 1
}

public sealed class Appointment
{
    private Appointment() { }

    public Guid Id { get; private set; }
    public Guid ProviderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Appointment> Book(Provider provider, Guid customerId, DateTime startUtc, DateTime nowUtc)
    {
        if (startUtc.Kind != DateTimeKind.Utc)
            return Error.Validation("Appointment.NotUtc", "Start time must be expressed in UTC.");
        if (startUtc <= nowUtc)
            return Error.Validation("Appointment.InPast", "Appointments must be booked in the future.");
        if (!provider.IsValidSlotStart(startUtc))
            return Error.Validation("Appointment.InvalidSlot", "Start time does not match one of the provider's slots.");

        return new Appointment
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            CustomerId = customerId,
            StartUtc = startUtc,
            EndUtc = startUtc + provider.SlotLength,
            Status = AppointmentStatus.Booked,
            CreatedAtUtc = nowUtc
        };
    }

    public Result Cancel(Guid requesterId, bool requesterIsAdmin, DateTime nowUtc)
    {
        if (!requesterIsAdmin && requesterId != CustomerId)
            return Error.Forbidden("Appointment.NotOwner", "You can only cancel your own appointments.");
        if (Status == AppointmentStatus.Cancelled)
            return Error.Conflict("Appointment.AlreadyCancelled", "Appointment is already cancelled.");
        if (StartUtc <= nowUtc)
            return Error.Validation("Appointment.AlreadyStarted", "Appointments that have started cannot be cancelled.");

        Status = AppointmentStatus.Cancelled;
        return Result.Success();
    }
}
