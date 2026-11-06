using AppointmentBooking.Domain.Entities;

namespace AppointmentBooking.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}

public interface IProviderRepository
{
    Task<Provider?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Provider>> ListAsync(CancellationToken ct);
    Task AddAsync(Provider provider, CancellationToken ct);
}

public interface IAppointmentRepository
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Appointment>> ListForCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<DateTime>> GetBookedStartsAsync(Guid providerId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    Task<bool> IsSlotTakenAsync(Guid providerId, DateTime startUtc, CancellationToken ct);
    Task AddAsync(Appointment appointment, CancellationToken ct);
}

public interface IUnitOfWork
{
    /// <summary>Persists pending changes.</summary>
    /// <exception cref="ConcurrencyConflictException">A unique constraint was violated by a concurrent writer.</exception>
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}

/// <summary>The authenticated caller, resolved from the HTTP context in the API layer.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
}

public sealed class ConcurrencyConflictException(string message, Exception? inner = null)
    : Exception(message, inner);
