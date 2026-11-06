using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Common;
using AppointmentBooking.Domain.Entities;
using FluentValidation;
using MediatR;

namespace AppointmentBooking.Application.Features.Appointments;

public sealed record AppointmentDto(
    Guid Id, Guid ProviderId, DateTime StartUtc, DateTime EndUtc, string Status)
{
    public static AppointmentDto From(Appointment a) =>
        new(a.Id, a.ProviderId, a.StartUtc, a.EndUtc, a.Status.ToString());
}

// ---- Book --------------------------------------------------------------------------------

public sealed record BookAppointmentCommand(Guid ProviderId, DateTime StartUtc) : IRequest<Result<AppointmentDto>>;

public sealed class BookAppointmentCommandValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.StartUtc).NotEmpty();
    }
}

public sealed class BookAppointmentCommandHandler(
    IProviderRepository providers,
    IAppointmentRepository appointments,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    TimeProvider clock) : IRequestHandler<BookAppointmentCommand, Result<AppointmentDto>>
{
    private static readonly Error SlotTaken =
        Error.Conflict("Appointment.SlotTaken", "This slot has already been booked.");

    public async Task<Result<AppointmentDto>> Handle(BookAppointmentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } customerId)
            return Error.Unauthorized("Auth.Required", "Authentication is required.");

        var provider = await providers.GetByIdAsync(request.ProviderId, ct);
        if (provider is null)
            return Error.NotFound("Provider.NotFound", "Provider was not found.");

        var booked = Appointment.Book(provider, customerId, request.StartUtc, clock.GetUtcNow().UtcDateTime);
        if (booked.IsFailure)
            return booked.Error!;

        // Fast path for the common case; the unique index below is the real guarantee under concurrency.
        if (await appointments.IsSlotTakenAsync(provider.Id, booked.Value.StartUtc, ct))
            return SlotTaken;

        await appointments.AddAsync(booked.Value, ct);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return SlotTaken;
        }

        return AppointmentDto.From(booked.Value);
    }
}

// ---- Cancel ------------------------------------------------------------------------------

public sealed record CancelAppointmentCommand(Guid AppointmentId) : IRequest<Result>;

public sealed class CancelAppointmentCommandHandler(
    IAppointmentRepository appointments,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    TimeProvider clock) : IRequestHandler<CancelAppointmentCommand, Result>
{
    public async Task<Result> Handle(CancelAppointmentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Error.Unauthorized("Auth.Required", "Authentication is required.");

        var appointment = await appointments.GetByIdAsync(request.AppointmentId, ct);
        if (appointment is null)
            return Error.NotFound("Appointment.NotFound", "Appointment was not found.");

        var result = appointment.Cancel(userId, currentUser.IsAdmin, clock.GetUtcNow().UtcDateTime);
        if (result.IsFailure)
            return result;

        await uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---- My appointments ---------------------------------------------------------------------

public sealed record ListMyAppointmentsQuery : IRequest<Result<IReadOnlyList<AppointmentDto>>>;

public sealed class ListMyAppointmentsQueryHandler(IAppointmentRepository appointments, ICurrentUser currentUser)
    : IRequestHandler<ListMyAppointmentsQuery, Result<IReadOnlyList<AppointmentDto>>>
{
    public async Task<Result<IReadOnlyList<AppointmentDto>>> Handle(
        ListMyAppointmentsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Error.Unauthorized("Auth.Required", "Authentication is required.");

        IReadOnlyList<AppointmentDto> items = (await appointments.ListForCustomerAsync(userId, ct))
            .Select(AppointmentDto.From)
            .ToList();
        return Result<IReadOnlyList<AppointmentDto>>.Success(items);
    }
}
