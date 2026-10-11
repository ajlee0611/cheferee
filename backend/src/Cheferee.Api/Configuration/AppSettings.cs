using System.ComponentModel.DataAnnotations;

namespace Cheferee.Api.Configuration;

public sealed record AppSettings
{
    [Required]
    public required DatabaseSettings Database { get; init; }

    [Required]
    public required RedisSettings Redis { get; init; }

    [Required]
    public required StorageSettings Storage { get; init; }

    [Required]
    public required JwtSettings Jwt { get; init; }
}

public sealed record DatabaseSettings
{
    [Required]
    public required string ConnectionString { get; init; }
}

public sealed record RedisSettings
{
    [Required]
    public required string ConnectionString { get; init; }
}

public sealed record StorageSettings
{
    [Required]
    public required string ServiceUrl { get; init; }

    [Required]
    public required string BucketName { get; init; }

    [Required]
    public required string AccessKey { get; init; }

    [Required]
    public required string SecretKey { get; init; }
}

public sealed record JwtSettings
{
    [Required, MinLength(32)]
    public required string Secret { get; init; }

    [Required]
    public required string Issuer { get; init; }

    [Required]
    public required string Audience { get; init; }

    [Range(1, 43200)]
    public int ExpirationMinutes { get; init; } = 10080; // 7 days default
}