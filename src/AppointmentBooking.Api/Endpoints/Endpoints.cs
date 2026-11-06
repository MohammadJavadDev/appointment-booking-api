using AppointmentBooking.Api.Infrastructure;
using AppointmentBooking.Application.Features.Appointments;
using AppointmentBooking.Application.Features.Auth;
using AppointmentBooking.Application.Features.Providers;
using MediatR;

namespace AppointmentBooking.Api.Endpoints;

public sealed record RegisterRequest(string Email, string FullName, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record CreateProviderRequest(string Name, string Specialty, TimeOnly DayStart, TimeOnly DayEnd, int SlotMinutes);
public sealed record BookAppointmentRequest(Guid ProviderId, DateTime StartUtc);

internal static class Endpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new RegisterCommand(r.Email, r.FullName, r.Password), ct))
                .ToHttp(auth => Results.Ok(auth)));

        group.MapPost("/login", async (LoginRequest r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new LoginCommand(r.Email, r.Password), ct))
                .ToHttp(auth => Results.Ok(auth)));

        return api;
    }

    public static RouteGroupBuilder MapProviderEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/providers").WithTags("Providers");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListProvidersQuery(), ct)));

        group.MapGet("/{id:guid}/slots", async (Guid id, DateOnly date, ISender sender, CancellationToken ct) =>
            (await sender.Send(new GetProviderSlotsQuery(id, date), ct)).ToHttp(slots => Results.Ok(slots)));

        group.MapPost("/", async (CreateProviderRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateProviderCommand(r.Name, r.Specialty, r.DayStart, r.DayEnd, r.SlotMinutes), ct))
                    .ToHttp(p => Results.Created($"/api/providers/{p.Id}", p)))
            .RequireAuthorization("Admin");

        return api;
    }

    public static RouteGroupBuilder MapAppointmentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/appointments").WithTags("Appointments").RequireAuthorization();

        group.MapPost("/", async (BookAppointmentRequest r, ISender sender, CancellationToken ct) =>
            (await sender.Send(new BookAppointmentCommand(r.ProviderId, r.StartUtc), ct))
                .ToHttp(a => Results.Created($"/api/appointments/{a.Id}", a)));

        group.MapGet("/mine", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new ListMyAppointmentsQuery(), ct)).ToHttp(list => Results.Ok(list)));

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            (await sender.Send(new CancelAppointmentCommand(id), ct)).ToHttp(() => Results.NoContent()));

        return api;
    }
}
