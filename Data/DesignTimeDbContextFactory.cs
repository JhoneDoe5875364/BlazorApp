using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCP.HRPortal.Data;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling at design time so migrations can be
/// scaffolded without a running database. SQLite needs no server, so this just
/// targets a throwaway file under the project's bin folder.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=hcp_hrportal.design.db")
            .Options;

        return new ApplicationDbContext(options);
    }
}
