using System.Security.Claims;
using Cheferee.Core.Entities;
using Cheferee.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cheferee.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // 1. Search Users (Fuzzy matching + Mutuals first)
        group.MapGet("/search", async (
            string? q,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Ok(Array.Empty<object>());

            var term = q.Trim().ToLower();
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId);

            // Fetch mutual IDs if authenticated
            var mutualUserIds = new HashSet<Guid>();
            if (currentUserId != Guid.Empty)
            {
                var followingIds = await db.Follows
                    .Where(f => f.FollowerId == currentUserId)
                    .Select(f => f.FolloweeId)
                    .ToListAsync();

                var mutuals = await db.Follows
                    .Where(f => f.FolloweeId == currentUserId && followingIds.Contains(f.FollowerId))
                    .Select(f => f.FollowerId)
                    .ToListAsync();

                mutualUserIds = mutuals.ToHashSet();
            }

            var matchingUsers = await db.Users
                .AsNoTracking()
                .Where(u => u.Id != currentUserId &&
                           (EF.Functions.ILike(u.Username, $"%{term}%") || EF.Functions.ILike(u.DisplayName, $"%{term}%")))
                .Take(25)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.DisplayName,
                    u.AvatarUrl,
                    u.Bio
                })
                .ToListAsync();

            // Order: Mutuals first, then alphabetical
            var ordered = matchingUsers
                .OrderByDescending(u => mutualUserIds.Contains(u.Id))
                .ThenBy(u => u.Username)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.DisplayName,
                    u.AvatarUrl,
                    u.Bio,
                    IsMutual = mutualUserIds.Contains(u.Id)
                });

            return Results.Ok(ordered);
        });

        // 2. Get User Profile by Username
        group.MapGet("/{username}", async (
            string username,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId);

            var user = await db.Users
                .AsNoTracking()
                .Include(u => u.Followers)
                .Include(u => u.Following)
                .Include(u => u.Posts)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null) return Results.NotFound(new { error = "User not found" });

            var isFollowing = currentUserId != Guid.Empty &&
                              user.Followers.Any(f => f.FollowerId == currentUserId);

            return Results.Ok(new
            {
                user.Id,
                user.Username,
                user.DisplayName,
                user.AvatarUrl,
                user.Bio,
                user.CreatedAt,
                PostsCount = user.Posts.Count,
                FollowersCount = user.Followers.Count,
                FollowingCount = user.Following.Count,
                IsFollowing = isFollowing
            });
        });

        // 3. Follow User
        group.MapPost("/{id:guid}/follow", async (
            Guid id,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            if (currentUserId == id)
                return Results.BadRequest(new { error = "You cannot follow yourself." });

            var targetExists = await db.Users.AnyAsync(u => u.Id == id);
            if (!targetExists) return Results.NotFound(new { error = "Target user does not exist." });

            var alreadyFollowing = await db.Follows.AnyAsync(f => f.FollowerId == currentUserId && f.FolloweeId == id);
            if (!alreadyFollowing)
            {
                db.Follows.Add(new Follow { FollowerId = currentUserId, FolloweeId = id });
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        }).RequireAuthorization();

        // 4. Unfollow User
        group.MapDelete("/{id:guid}/follow", async (
            Guid id,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            var follow = await db.Follows.FirstOrDefaultAsync(f => f.FollowerId == currentUserId && f.FolloweeId == id);
            if (follow != null)
            {
                db.Follows.Remove(follow);
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        }).RequireAuthorization();

        return group;
    }
}