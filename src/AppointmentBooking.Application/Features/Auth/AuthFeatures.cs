using AppointmentBooking.Application.Abstractions;
using AppointmentBooking.Domain.Common;
using AppointmentBooking.Domain.Entities;
using FluentValidation;
using MediatR;

namespace AppointmentBooking.Application.Features.Auth;

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, Guid UserId, string Role);

// ---- Register ----------------------------------------------------------------------------

public sealed record RegisterCommand(string Email, string FullName, string Password) : IRequest<Result<AuthResponse>>;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}

public sealed class RegisterCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtTokenGenerator tokens,
    IUnitOfWork uow) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        if (await users.EmailExistsAsync(request.Email.Trim().ToLowerInvariant(), ct))
            return Error.Conflict("Auth.EmailTaken", "An account with this email already exists.");

        var created = User.Create(request.Email, request.FullName, hasher.Hash(request.Password), UserRole.Customer);
        if (created.IsFailure)
            return created.Error!;

        await users.AddAsync(created.Value, ct);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            return Error.Conflict("Auth.EmailTaken", "An account with this email already exists.");
        }

        var token = tokens.Generate(created.Value);
        return new AuthResponse(token.Value, token.ExpiresAtUtc, created.Value.Id, created.Value.Role.ToString());
    }
}

// ---- Login -------------------------------------------------------------------------------

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtTokenGenerator tokens) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);

        // Same message for unknown user and wrong password: do not reveal which accounts exist.
        if (user is null || !hasher.Verify(user.PasswordHash, request.Password))
            return Error.Unauthorized("Auth.InvalidCredentials", "Email or password is incorrect.");

        var token = tokens.Generate(user);
        return new AuthResponse(token.Value, token.ExpiresAtUtc, user.Id, user.Role.ToString());
    }
}
