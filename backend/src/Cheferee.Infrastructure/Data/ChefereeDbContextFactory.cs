using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cheferee.Infrastructure.Data;

public sealed class ChefereeDbContextFactory : IDesignTimeDbContextFactory<ChefereeDbContext>
{
    public ChefereeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChefereeDbContext>();
        
        // Design-time fallback connection string pointing directly to our local dev container
        var connectionString = "Host=localhost;Port=5432;Database=cheferee_dev;Username=cheferee_user;Password=cheferee_dev_password";

        optionsBuilder.UseNpgsql(connectionString);

        return new ChefereeDbContext(optionsBuilder.Options);
    }
}