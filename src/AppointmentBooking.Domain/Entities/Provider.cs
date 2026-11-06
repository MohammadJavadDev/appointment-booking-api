using AppointmentBooking.Domain.Common;

namespace AppointmentBooking.Domain.Entities;

/// <summary>
/// A bookable service provider (doctor, stylist, ...). Working hours are expressed in UTC
/// to keep the sample free of time-zone concerns.
/// </summary>
public sealed class Provider
{
    private Provider() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Specialty { get; private set; } = string.Empty;
    public TimeOnly DayStart { get; private set; }
    public TimeOnly DayEnd { get; private set; }
    public int SlotMinutes { get; private set; }

    public TimeSpan SlotLength => TimeSpan.FromMinutes(SlotMinutes);

    public static Result<Provider> Create(
        string name, string specialty, TimeOnly dayStart, TimeOnly dayEnd, int slotMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Error.Validation("Provider.NameRequired", "Provider name is required.");
        if (slotMinutes is < 5 or > 240)
            return Error.Validation("Provider.InvalidSlot", "Slot length must be between 5 and 240 minutes.");
        if (dayEnd <= dayStart)
            return Error.Validation("Provider.InvalidHours", "Working day must end after it starts.");
        if ((dayEnd - dayStart).TotalMinutes < slotMinutes)
            return Error.Validation("Provider.InvalidHours", "Working day is shorter than one slot.");

        return new Provider
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Specialty = (specialty ?? string.Empty).Trim(),
            DayStart = dayStart,
            DayEnd = dayEnd,
            SlotMinutes = slotMinutes
        };
    }

    /// <summary>All slot start times (UTC) for the given day.</summary>
    public IReadOnlyList<DateTime> SlotsFor(DateOnly date)
    {
        var slots = new List<DateTime>();
        var cursor = date.ToDateTime(DayStart, DateTimeKind.Utc);
        var end = date.ToDateTime(DayEnd, DateTimeKind.Utc);

        while (cursor + SlotLength <= end)
        {
            slots.Add(cursor);
            cursor += SlotLength;
        }

        return slots;
    }

    /// <summary>True when <paramref name="startUtc"/> is exactly the start of one of the provider's slots.</summary>
    public bool IsValidSlotStart(DateTime startUtc)
    {
        var date = DateOnly.FromDateTime(startUtc);
        return SlotsFor(date).Contains(startUtc);
    }
}
