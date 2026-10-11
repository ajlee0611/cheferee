namespace Cheferee.Core.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<Post> Posts { get; set; } = [];
    public ICollection<PostComment> Comments { get; set; } = [];
    public ICollection<PostReaction> Reactions { get; set; } = [];
    public ICollection<Follow> Followers { get; set; } = []; // People who follow this user
    public ICollection<Follow> Following { get; set; } = []; // People this user follows
}