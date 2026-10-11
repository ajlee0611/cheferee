using System.Security.Claims;
using Cheferee.Core.Entities;
using Cheferee.Core.Enums;
using Cheferee.Infrastructure.Data;
using Cheferee.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cheferee.Api.Endpoints;

public static class PostEndpoints
{
    public record UploadIntentRequest(string ContentType);
    public record CreatePostRequest(string ImageUrl, string Caption);
    public record AddReactionRequest(ReactionType Type);
    public record AddCommentRequest(string Content);

    public static RouteGroupBuilder MapPostEndpoints(this RouteGroupBuilder group)
    {
        // 1. Pre-Signed S3 Upload Intent
        group.MapPost("/upload-intent", (
            [FromBody] UploadIntentRequest request,
            IStorageService storageService) =>
        {
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(request.ContentType.ToLower()))
                return Results.BadRequest(new { error = "Unsupported media format. Allowed: jpeg, png, webp." });

            var extension = request.ContentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => ".webp"
            };

            var objectKey = $"meals/{Guid.NewGuid()}{extension}";
            var uploadUrl = storageService.GeneratePreSignedUploadUrl(objectKey, request.ContentType, TimeSpan.FromMinutes(10));
            var publicUrl = storageService.GetPublicUrl(objectKey);

            return Results.Ok(new { uploadUrl, publicUrl, objectKey });
        }).RequireAuthorization();

        group.MapPut("/upload-direct", async (
            [FromQuery] string file,
            HttpRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(file))
                return Results.BadRequest(new { error = "File query parameter is missing." });

            var targetFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "media");
            Directory.CreateDirectory(targetFolder);

            var filePath = Path.Combine(targetFolder, Path.GetFileName(file));
            using var fileStream = File.Create(filePath);
            await request.Body.CopyToAsync(fileStream);

            return Results.Ok(new { status = "uploaded", file });
        });

        // 2. Create Post
        group.MapPost("/", async (
            [FromBody] CreatePostRequest request,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.ImageUrl))
                return Results.BadRequest(new { error = "Image URL is required." });

            var post = new Post
            {
                AuthorId = currentUserId,
                ImageUrl = request.ImageUrl.Trim(),
                Caption = request.Caption?.Trim() ?? string.Empty
            };

            db.Posts.Add(post);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/posts/{post.Id}", new
            {
                post.Id,
                post.AuthorId,
                post.ImageUrl,
                post.Caption,
                post.CreatedAt
            });
        }).RequireAuthorization();

        // 3. Chronological Friends-Only Feed
        group.MapGet("/feed", async (
            ChefereeDbContext db,
            ClaimsPrincipal principal,
            int page = 1,
            int pageSize = 15) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            var followedIds = await db.Follows
                .Where(f => f.FollowerId == currentUserId)
                .Select(f => f.FolloweeId)
                .ToListAsync();

            // Include user's own posts plus all accounts they follow
            var feedTargetIds = followedIds.Append(currentUserId).Distinct().ToList();

            var posts = await db.Posts
                .AsNoTracking()
                .Where(p => feedTargetIds.Contains(p.AuthorId))
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.ImageUrl,
                    p.Caption,
                    p.CreatedAt,
                    p.CommentsCount,
                    p.ReactionsCount,
                    Author = new
                    {
                        p.Author.Id,
                        p.Author.Username,
                        p.Author.DisplayName,
                        p.Author.AvatarUrl
                    },
                    UserReaction = p.Reactions
                        .Where(r => r.UserId == currentUserId)
                        .Select(r => r.Type.ToString())
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Results.Ok(posts);
        }).RequireAuthorization();

        // 4. User Profile Posts Grid
        group.MapGet("/user/{username}", async (
            string username,
            ChefereeDbContext db,
            int page = 1,
            int pageSize = 18) =>
        {
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
            if (user == null) return Results.NotFound(new { error = "User not found" });

            var posts = await db.Posts
                .AsNoTracking()
                .Where(p => p.AuthorId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.ImageUrl,
                    p.Caption,
                    p.CreatedAt,
                    p.CommentsCount,
                    p.ReactionsCount
                })
                .ToListAsync();

            return Results.Ok(posts);
        });

        // 5. Atomic Reaction Ruling (Toggle / Upsert)
        group.MapPost("/{id:guid}/reactions", async (
            Guid id,
            [FromBody] AddReactionRequest request,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return Results.NotFound(new { error = "Post not found" });

            var existingReaction = await db.PostReactions.FirstOrDefaultAsync(r => r.PostId == id && r.UserId == currentUserId);

            if (existingReaction != null)
            {
                if (existingReaction.Type == request.Type)
                {
                    // Toggle off if same card clicked
                    db.PostReactions.Remove(existingReaction);
                    post.ReactionsCount = Math.Max(0, post.ReactionsCount - 1);
                    await db.SaveChangesAsync();
                    return Results.Ok(new { status = "removed", currentReaction = (string?)null, post.ReactionsCount });
                }

                // Switch card ruling
                existingReaction.Type = request.Type;
                await db.SaveChangesAsync();
                return Results.Ok(new { status = "updated", currentReaction = request.Type.ToString(), post.ReactionsCount });
            }

            // New card ruling
            db.PostReactions.Add(new PostReaction
            {
                PostId = id,
                UserId = currentUserId,
                Type = request.Type
            });
            post.ReactionsCount += 1;
            await db.SaveChangesAsync();

            return Results.Ok(new { status = "added", currentReaction = request.Type.ToString(), post.ReactionsCount });
        }).RequireAuthorization();

        // 6. Post Comments
        group.MapGet("/{id:guid}/comments", async (
            Guid id,
            ChefereeDbContext db,
            int page = 1,
            int pageSize = 20) =>
        {
            var comments = await db.PostComments
                .AsNoTracking()
                .Where(c => c.PostId == id)
                .OrderBy(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Content,
                    c.CreatedAt,
                    Author = new
                    {
                        c.Author.Id,
                        c.Author.Username,
                        c.Author.DisplayName,
                        c.Author.AvatarUrl
                    }
                })
                .ToListAsync();

            return Results.Ok(comments);
        });

        group.MapPost("/{id:guid}/comments", async (
            Guid id,
            [FromBody] AddCommentRequest request,
            ChefereeDbContext db,
            ClaimsPrincipal principal) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
                return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Content))
                return Results.BadRequest(new { error = "Comment cannot be empty." });

            var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return Results.NotFound(new { error = "Post not found" });

            var comment = new PostComment
            {
                PostId = id,
                AuthorId = currentUserId,
                Content = request.Content.Trim()
            };

            db.PostComments.Add(comment);
            post.CommentsCount += 1;
            await db.SaveChangesAsync();

            var author = await db.Users.AsNoTracking().FirstAsync(u => u.Id == currentUserId);

            return Results.Created($"/api/v1/posts/{id}/comments/{comment.Id}", new
            {
                comment.Id,
                comment.Content,
                comment.CreatedAt,
                Author = new
                {
                    author.Id,
                    author.Username,
                    author.DisplayName,
                    author.AvatarUrl
                }
            });
        }).RequireAuthorization();

        return group;
    }
}