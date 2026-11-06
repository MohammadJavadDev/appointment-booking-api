using AppointmentBooking.Domain.Common;
using AppointmentBooking.Domain.Entities;
using FluentAssertions;

namespace AppointmentBooking.Domain.Tests;

public class ProviderTests
{
    private static Provider NewProvider(int slotMinutes = 30) =>
        Provider.Create("Dr. Test", "General", new TimeOnly(9, 0), new TimeOnly(12, 0), slotMinutes).Value;

    [Fact]
    public void Create_with_valid_data_succeeds()
    {
        var result = Provider.Create("Dr. Test", "General", new TimeOnly(9, 0), new TimeOnly(17, 0), 30);

        result.IsSuccess.Should().BeTrue();
        result.Value.SlotLength.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Theory]
    [InlineData("", 30, 9, 17)]
    [InlineData("Dr. Test", 1, 9, 17)]
    [InlineData("Dr. Test", 500, 9, 17)]
    [InlineData("Dr. Test", 30, 17, 9)]
    public void Create_with_invalid_data_fails(string name, int slot, int startHour, int endHour)
    {
        var result = Provider.Create(name, "x", new TimeOnly(startHour, 0), new TimeOnly(endHour, 0), slot);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void SlotsFor_returns_back_to_back_slots_within_working_hours()
    {
        var slots = NewProvider(60).SlotsFor(new DateOnly(2030, 1, 1));

        slots.Should().HaveCount(3);
        slots[0].Should().Be(new DateTime(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc));
        slots[^1].Should().Be(new DateTime(2030, 1, 1, 11, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void SlotsFor_drops_a_trailing_partial_slot()
    {
        // 180 minutes / 70 = 2 full slots; the remaining 40 minutes cannot hold a slot.
        NewProvider(70).SlotsFor(new DateOnly(2030, 1, 1)).Should().HaveCount(2);
    }
}

public class AppointmentTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Customer = Guid.NewGuid();

    private static Provider Provider() =>
        Domain.Entities.Provider.Create("Dr. Test", "General", new TimeOnly(9, 0), new TimeOnly(12, 0), 30).Value;

    private static DateTime Utc(int hour, int minute = 0) => new(2030, 1, 2, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void Book_on_a_valid_slot_succeeds()
    {
        var result = Appointment.Book(Provider(), Customer, Utc(9, 30), Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.EndUtc.Should().Be(Utc(10));
        result.Value.Status.Should().Be(AppointmentStatus.Booked);
    }

    [Fact]
    public void Book_in_the_past_fails()
    {
        var result = Appointment.Book(Provider(), Customer, new DateTime(2029, 12, 31, 9, 0, 0, DateTimeKind.Utc), Now);

        result.Error!.Code.Should().Be("Appointment.InPast");
    }

    [Fact]
    public void Book_off_the_slot_grid_fails()
    {
        Appointment.Book(Provider(), Customer, Utc(9, 10), Now).Error!.Code.Should().Be("Appointment.InvalidSlot");
    }

    [Fact]
    public void Book_outside_working_hours_fails()
    {
        Appointment.Book(Provider(), Customer, Utc(14), Now).Error!.Code.Should().Be("Appointment.InvalidSlot");
    }

    [Fact]
    public void Book_with_a_non_utc_time_fails()
    {
        var local = new DateTime(2030, 1, 2, 9, 0, 0, DateTimeKind.Unspecified);

        Appointment.Book(Provider(), Customer, local, Now).Error!.Code.Should().Be("Appointment.NotUtc");
    }

    [Fact]
    public void Owner_can_cancel()
    {
        var appointment = Appointment.Book(Provider(), Customer, Utc(9), Now).Value;

        appointment.Cancel(Customer, false, Now).IsSuccess.Should().BeTrue();
        appointment.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Admin_can_cancel_someone_elses_appointment()
    {
        var appointment = Appointment.Book(Provider(), Customer, Utc(9), Now).Value;

        appointment.Cancel(Guid.NewGuid(), true, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Another_customer_cannot_cancel()
    {
        var appointment = Appointment.Book(Provider(), Customer, Utc(9), Now).Value;

        var result = appointment.Cancel(Guid.NewGuid(), false, Now);

        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        appointment.Status.Should().Be(AppointmentStatus.Booked);
    }

    [Fact]
    public void Cancelling_twice_is_a_conflict()
    {
        var appointment = Appointment.Book(Provider(), Customer, Utc(9), Now).Value;
        appointment.Cancel(Customer, false, Now);

        appointment.Cancel(Customer, false, Now).Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Cannot_cancel_after_the_appointment_started()
    {
        var appointment = Appointment.Book(Provider(), Customer, Utc(9), Now).Value;

        appointment.Cancel(Customer, false, Utc(9, 5)).Error!.Code.Should().Be("Appointment.AlreadyStarted");
    }
}
