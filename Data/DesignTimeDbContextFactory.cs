using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCP.HRPortal.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling at design time so migrations can be
/// scaffolded without a running MySQL server. The server version is pinned (no
/// AutoDetect) precisely so no live connection is needed to generate migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(
                "Server=localhost;Database=hcp_hrportal;User=root;Password=root;",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        return new ApplicationDbContext(options);
    }
}
