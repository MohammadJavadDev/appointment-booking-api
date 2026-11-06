using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Common;
using AppointmentBooking.Domain.Entities;
using FluentValidation;
using MediatR;

namespace AppointmentBooking.Application.Features.Providers;

public sealed record ProviderDto(
    Guid Id, string Name, string Specialty, TimeOnly DayStart, TimeOnly DayEnd, int SlotMinutes)
{
    public static ProviderDto From(Provider p) =>
        new(p.Id, p.Name, p.Specialty, p.DayStart, p.DayEnd, p.SlotMinutes);
}

public sealed record SlotDto(DateTime StartUtc, DateTime EndUtc, bool IsAvailable);

// ---- Create (admin) ----------------------------------------------------------------------

public sealed record CreateProviderCommand(
    string Name, string Specialty, TimeOnly DayStart, TimeOnly DayEnd, int SlotMinutes)
    : IRequest<Result<ProviderDto>>;

public sealed class CreateProviderCommandValidator : AbstractValidator<CreateProviderCommand>
{
    public CreateProviderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Specialty).MaximumLength(200);
        RuleFor(x => x.SlotMinutes).InclusiveBetween(5, 240);
        RuleFor(x => x.DayEnd).GreaterThan(x => x.DayStart);
    }
}

public sealed class CreateProviderCommandHandler(IProviderRepository providers, IUnitOfWork uow)
    : IRequestHandler<CreateProviderCommand, Result<ProviderDto>>
{
    public async Task<Result<ProviderDto>> Handle(CreateProviderCommand request, CancellationToken ct)
    {
        var created = Provider.Create(
            request.Name, request.Specialty, request.DayStart, request.DayEnd, request.SlotMinutes);
        if (created.IsFailure)
            return created.Error!;

        await providers.AddAsync(created.Value, ct);
        await uow.SaveChangesAsync(ct);
        return ProviderDto.From(created.Value);
    }
}

// ---- List --------------------------------------------------------------------------------

public sealed record ListProvidersQuery : IRequest<IReadOnlyList<ProviderDto>>;

public sealed class ListProvidersQueryHandler(IProviderRepository providers)
    : IRequestHandler<ListProvidersQuery, IReadOnlyList<ProviderDto>>
{
    public async Task<IReadOnlyList<ProviderDto>> Handle(ListProvidersQuery request, CancellationToken ct) =>
        (await providers.ListAsync(ct)).Select(ProviderDto.From).ToList();
}

// ---- Slots for a day ---------------------------------------------------------------------

public sealed record GetProviderSlotsQuery(Guid ProviderId, DateOnly Date) : IRequest<Result<IReadOnlyList<SlotDto>>>;

public sealed class GetProviderSlotsQueryHandler(
    IProviderRepository providers,
    IAppointmentRepository appointments,
    TimeProvider clock) : IRequestHandler<GetProviderSlotsQuery, Result<IReadOnlyList<SlotDto>>>
{
    public async Task<Result<IReadOnlyList<SlotDto>>> Handle(GetProviderSlotsQuery request, CancellationToken ct)
    {
        var provider = await providers.GetByIdAsync(request.ProviderId, ct);
        if (provider is null)
            return Error.NotFound("Provider.NotFound", "Provider was not found.");

        var dayStart = request.Date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var booked = (await appointments.GetBookedStartsAsync(provider.Id, dayStart, dayStart.AddDays(1), ct))
            .ToHashSet();
        var now = clock.GetUtcNow().UtcDateTime;

        IReadOnlyList<SlotDto> slots = provider.SlotsFor(request.Date)
            .Select(start => new SlotDto(start, start + provider.SlotLength, start > now && !booked.Contains(start)))
            .ToList();

        return Result<IReadOnlyList<SlotDto>>.Success(slots);
    }
}
