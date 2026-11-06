using AppointmentBooking.Domain.Common;

namespace AppointmentBooking.Domain.Entities;

public enum UserRole
{
    Customer = 0,
    Admin = 1
}

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    public static Result<User> Create(string email, string fullName, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Error.Validation("User.EmailRequired", "Email is required.");
        if (string.IsNullOrWhiteSpace(fullName))
            return Error.Validation("User.NameRequired", "Full name is required.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash,
            Role = role
        };
    }
}
