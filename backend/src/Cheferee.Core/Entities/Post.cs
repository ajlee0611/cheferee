namespace Cheferee.Core.Entities;

public sealed class Post
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public required string ImageUrl { get; set; }
    public string Caption { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Aggregated counter caches for performant O(1) grid reads
    public int CommentsCount { get; set; }
    public int ReactionsCount { get; set; }

    // Navigation properties
    public ICollection<PostComment> Comments { get; set; } = [];
    public ICollection<PostReaction> Reactions { get; set; } = [];
}