using EdilManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EdilManager.Desktop;

// Usata solo dai comandi `dotnet ef` (es. migrations add).
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
