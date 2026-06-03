using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CompGateApi.Data.Context
{
    public class CompGateDbContextFactory : IDesignTimeDbContextFactory<CompGateApiDbContext>
    {
        public CompGateApiDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("COMPGATE_MIGRATION_CONNECTION")
                ?? "Data Source=tcp:10.3.3.11;Database=CompGateDb;User Id=ccadmin;Password=ccadmin;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<CompGateApiDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new CompGateApiDbContext(options);
        }
    }
}
