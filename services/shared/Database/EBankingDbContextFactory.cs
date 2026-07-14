using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Shared.Database;

public class EBankingDbContextFactory : IDesignTimeDbContextFactory<EBankingDbContext>
{
    public EBankingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EBankingDbContext>();
        
        // Use the connection string for design-time operations
        var connectionString = "Server=localhost,1433;Database=EBanking;User Id=sa;Password=Str0ng!Passw0rd1!;TrustServerCertificate=true;";
        optionsBuilder.UseSqlServer(connectionString);
        
        return new EBankingDbContext(optionsBuilder.Options);
    }
}