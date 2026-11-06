using System.Net;
using System.Net.Http.Json;
using AppointmentBooking.Application.Features.Appointments;
using AppointmentBooking.Application.Features.Providers;
using FluentAssertions;

namespace AppointmentBooking.Api.IntegrationTests;

public class BookingFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Provider works 09:00-12:00 UTC in 30 minute slots => 6 slots per day.
    private static async Task<ProviderDto> CreateProviderAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/providers", new
        {
            name = $"Dr. {Guid.NewGuid():N}",
            specialty = "General",
            dayStart = "09:00:00",
            dayEnd = "12:00:00",
            slotMinutes = 30
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProviderDto>())!;
    }

    private static DateTime Tomorrow(int hour, int minute = 0)
    {
        var day = DateTime.UtcNow.Date.AddDays(1);
        return new DateTime(day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Utc);
    }

    [Fact]
    public async Task Protected_endpoints_require_authentication()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/appointments/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.PostAsJsonAsync("/api/providers", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Customers_cannot_create_providers()
    {
        var customer = await factory.CreateCustomerClientAsync();

        var response = await customer.PostAsJsonAsync("/api/providers", new
        {
            name = "Nope", specialty = "x", dayStart = "09:00:00", dayEnd = "12:00:00", slotMinutes = 30
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_requests_return_validation_problems()
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/providers", new
        {
            name = "", specialty = "x", dayStart = "09:00:00", dayEnd = "12:00:00", slotMinutes = 30
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_registration_and_bad_login_are_rejected()
    {
        var client = factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.local";
        var body = new { email, fullName = "Dup", password = "Password123!" };

        (await client.PostAsJsonAsync("/api/auth/register", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/register", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_customer_can_book_see_and_cancel_an_appointment()
    {
        var admin = await factory.CreateAdminClientAsync();
        var customer = await factory.CreateCustomerClientAsync();
        var provider = await CreateProviderAsync(admin);
        var start = Tomorrow(9, 30);

        var book = await customer.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = start });
        book.StatusCode.Should().Be(HttpStatusCode.Created);
        var appointment = (await book.Content.ReadFromJsonAsync<AppointmentDto>())!;

        var mine = await customer.GetFromJsonAsync<List<AppointmentDto>>("/api/appointments/mine");
        mine.Should().ContainSingle(a => a.Id == appointment.Id && a.Status == "Booked");

        var slots = await customer.GetFromJsonAsync<List<SlotDto>>(
            $"/api/providers/{provider.Id}/slots?date={DateOnly.FromDateTime(start):yyyy-MM-dd}");
        slots.Should().NotBeNull().And.HaveCount(6);
        slots!.Single(s => s.StartUtc == start).IsAvailable.Should().BeFalse();
        slots!.Count(s => s.IsAvailable).Should().Be(5);

        (await customer.DeleteAsync($"/api/appointments/{appointment.Id}")).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        // The freed slot can be booked again.
        var rebook = await customer.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = start });
        rebook.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Booking_an_off_grid_time_is_rejected()
    {
        var admin = await factory.CreateAdminClientAsync();
        var customer = await factory.CreateCustomerClientAsync();
        var provider = await CreateProviderAsync(admin);

        var response = await customer.PostAsJsonAsync(
            "/api/appointments", new { providerId = provider.Id, startUtc = Tomorrow(9, 10) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Booking_a_taken_slot_returns_conflict()
    {
        var admin = await factory.CreateAdminClientAsync();
        var first = await factory.CreateCustomerClientAsync();
        var second = await factory.CreateCustomerClientAsync();
        var provider = await CreateProviderAsync(admin);
        var start = Tomorrow(10);

        (await first.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = start }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await second.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = start }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Customers_cannot_cancel_each_others_appointments()
    {
        var admin = await factory.CreateAdminClientAsync();
        var owner = await factory.CreateCustomerClientAsync();
        var other = await factory.CreateCustomerClientAsync();
        var provider = await CreateProviderAsync(admin);

        var book = await owner.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = Tomorrow(11) });
        var appointment = (await book.Content.ReadFromJsonAsync<AppointmentDto>())!;

        (await other.DeleteAsync($"/api/appointments/{appointment.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.DeleteAsync($"/api/appointments/{appointment.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Concurrent_bookings_for_one_slot_produce_exactly_one_winner()
    {
        var admin = await factory.CreateAdminClientAsync();
        var provider = await CreateProviderAsync(admin);
        var start = Tomorrow(11, 30);

        var customers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => factory.CreateCustomerClientAsync()));
        var responses = await Task.WhenAll(customers.Select(c =>
            c.PostAsJsonAsync("/api/appointments", new { providerId = provider.Id, startUtc = start })));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(7);
    }

    [Fact]
    public async Task Unknown_provider_returns_not_found()
    {
        var customer = await factory.CreateCustomerClientAsync();

        var response = await customer.GetAsync($"/api/providers/{Guid.NewGuid()}/slots?date=2030-01-01");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        (await factory.CreateClient().GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
