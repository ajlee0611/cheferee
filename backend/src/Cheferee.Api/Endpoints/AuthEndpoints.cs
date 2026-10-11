using System.ComponentModel.DataAnnotations;
using Cheferee.Core.Entities;
using Cheferee.Infrastructure.Data;
using Cheferee.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cheferee.Api.Endpoints;

public static class AuthEndpoints
{
    public record RegisterRequest(
        [Required, MinLength(3), MaxLength(32)] string Username,
        [Required, MaxLength(64)] string DisplayName,
        [Required, EmailAddress] string Email,
        [Required, MinLength(6)] string Password);

    public record LoginRequest(
        [Required] string UsernameOrEmail,
        [Required] string Password);

    public record AuthResponse(Guid Id, string Username, string DisplayName, string Email, string? AvatarUrl, string Token);

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            ChefereeDbContext db,
            ITokenService tokenService) =>
        {
            var normalizedUsername = request.Username.Trim().ToLowerInvariant();
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            if (await db.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername || u.Email.ToLower() == normalizedEmail))
            {
                return Results.Conflict(new { error = "Username or email is already registered." });
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                Username = request.Username.Trim(),
                DisplayName = request.DisplayName.Trim(),
                Email = request.Email.Trim(),
                PasswordHash = passwordHash
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var token = tokenService.GenerateToken(user);
            return Results.Created($"/api/v1/users/{user.Username}", new AuthResponse(
                user.Id, user.Username, user.DisplayName, user.Email, user.AvatarUrl, token
            ));
        });

        group.MapPost("/login", async (
            [FromBody] LoginRequest request,
            ChefereeDbContext db,
            ITokenService tokenService) =>
        {
            var identifier = request.UsernameOrEmail.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u =>
                u.Username.ToLower() == identifier || u.Email.ToLower() == identifier);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var token = tokenService.GenerateToken(user);
            return Results.Ok(new AuthResponse(
                user.Id, user.Username, user.DisplayName, user.Email, user.AvatarUrl, token
            ));
        });

        return group;
    }
}