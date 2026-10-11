using Cheferee.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Cheferee.Infrastructure.Data;

public sealed class ChefereeDbContext : DbContext
{
    public ChefereeDbContext(DbContextOptions<ChefereeDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostReaction> PostReactions => Set<PostReaction>();
    public DbSet<PostComment> PostComments => Set<PostComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Enable PostgreSQL trigram extension for fuzzy username matching
        modelBuilder.HasPostgresExtension("pg_trgm");

        // 2. User Configuration
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.HasIndex(u => u.Username).IsUnique();
            builder.HasIndex(u => u.Email).IsUnique();

            builder.Property(u => u.Username).HasMaxLength(32).IsRequired();
            builder.Property(u => u.DisplayName).HasMaxLength(64).IsRequired();
            builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
            builder.Property(u => u.Bio).HasMaxLength(200);

            // GIN Trigram index for ultra-fast search-as-you-type on username & display name
            builder.HasIndex(u => new { u.Username, u.DisplayName })
                   .HasMethod("gin")
                   .HasOperators("gin_trgm_ops", "gin_trgm_ops");
        });

        // 3. Follow Configuration (Asymmetric Social Graph)
        modelBuilder.Entity<Follow>(builder =>
        {
            builder.HasKey(f => new { f.FollowerId, f.FolloweeId });

            builder.HasOne(f => f.Follower)
                   .WithMany(u => u.Following)
                   .HasForeignKey(f => f.FollowerId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(f => f.Followee)
                   .WithMany(u => u.Followers)
                   .HasForeignKey(f => f.FolloweeId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(f => f.FolloweeId);
        });

        // 4. Post Configuration
        modelBuilder.Entity<Post>(builder =>
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.ImageUrl).IsRequired();
            builder.Property(p => p.Caption).HasMaxLength(500);

            builder.HasOne(p => p.Author)
                   .WithMany(u => u.Posts)
                   .HasForeignKey(p => p.AuthorId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Critical index: fast queries for "all posts by user, newest first" (profile grid)
            builder.HasIndex(p => new { p.AuthorId, p.CreatedAt });

            // Critical index: fast home feed pagination
            builder.HasIndex(p => p.CreatedAt);
        });

        // 5. PostReaction Configuration
        modelBuilder.Entity<PostReaction>(builder =>
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Type)
                   .HasConversion<string>()
                   .HasMaxLength(24);

            builder.HasOne(r => r.Post)
                   .WithMany(p => p.Reactions)
                   .HasForeignKey(r => r.PostId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.User)
                   .WithMany(u => u.Reactions)
                   .HasForeignKey(r => r.UserId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Constraint: A user can only have ONE reaction per post
            builder.HasIndex(r => new { r.PostId, r.UserId }).IsUnique();
        });

        // 6. PostComment Configuration
        modelBuilder.Entity<PostComment>(builder =>
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Content).HasMaxLength(500).IsRequired();

            builder.HasOne(c => c.Post)
                   .WithMany(p => p.Comments)
                   .HasForeignKey(c => c.PostId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.Author)
                   .WithMany(u => u.Comments)
                   .HasForeignKey(c => c.AuthorId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => new { c.PostId, c.CreatedAt });
        });
    }
}