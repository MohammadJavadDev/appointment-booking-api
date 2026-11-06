using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AppointmentBooking.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Creates the schema and, when <c>Seed:AdminEmail</c>/<c>Seed:AdminPassword</c> are configured,
    /// an initial admin account. A real deployment would apply EF Core migrations instead of EnsureCreated.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        await db.Database.EnsureCreatedAsync(ct);

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var normalized = email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
            return;

        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var admin = User.Create(email, "Administrator", hasher.Hash(password), UserRole.Admin);
        db.Users.Add(admin.Value);
        await db.SaveChangesAsync(ct);
    }
}
