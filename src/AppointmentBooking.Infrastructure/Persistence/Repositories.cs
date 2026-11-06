using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppointmentBooking.Infrastructure.Persistence;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);
}

internal sealed class ProviderRepository(AppDbContext db) : IProviderRepository
{
    public Task<Provider?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Providers.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Provider>> ListAsync(CancellationToken ct) =>
        await db.Providers.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task AddAsync(Provider provider, CancellationToken ct) => await db.Providers.AddAsync(provider, ct);
}

internal sealed class AppointmentRepository(AppDbContext db) : IAppointmentRepository
{
    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListForCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Appointments.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderBy(a => a.StartUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(
        Guid providerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await db.Appointments.AsNoTracking()
            .Where(a => a.ProviderId == providerId
                        && a.Status == AppointmentStatus.Booked
                        && a.StartUtc >= fromUtc && a.StartUtc < toUtc)
            .Select(a => a.StartUtc)
            .ToListAsync(ct);

    public Task<bool> IsSlotTakenAsync(Guid providerId, DateTime startUtc, CancellationToken ct) =>
        db.Appointments.AnyAsync(
            a => a.ProviderId == providerId && a.StartUtc == startUtc && a.Status == AppointmentStatus.Booked, ct);

    public async Task AddAsync(Appointment appointment, CancellationToken ct) =>
        await db.Appointments.AddAsync(appointment, ct);
}
