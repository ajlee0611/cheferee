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