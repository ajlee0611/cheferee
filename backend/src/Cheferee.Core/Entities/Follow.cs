namespace Cheferee.Core.Entities;

public sealed class Follow
{
    public Guid FollowerId { get; set; }
    public User Follower { get; set; } = null!;

    public Guid FolloweeId { get; set; }
    public User Followee { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}