using System.Net.Http.Headers;
using System.Net.Http.Json;
using AppointmentBooking.Application.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AppointmentBooking.Api.IntegrationTests;

/// <summary>Boots the real API against a throw-away SQLite database file.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "AdminPass123!";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"booking-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password, bool register)
    {
        var client = CreateClient();
        var response = register
            ? await client.PostAsJsonAsync("/api/auth/register", new { email, fullName = "Test User", password })
            : await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() =>
        CreateAuthenticatedClientAsync(AdminEmail, AdminPassword, register: false);

    public Task<HttpClient> CreateCustomerClientAsync() =>
        CreateAuthenticatedClientAsync($"{Guid.NewGuid():N}@test.local", "CustomerPass123!", register: true);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
        {
            try { File.Delete(file); } catch (IOException) { /* best effort */ }
        }
    }
}
