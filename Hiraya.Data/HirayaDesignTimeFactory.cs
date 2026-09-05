using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hiraya.Data;

public class HirayaDesignTimeFactory : IDesignTimeDbContextFactory<HirayaLearningCenterDbContext>
{
    public HirayaLearningCenterDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("HIRAYA_MYSQL")
                         ?? "Server=127.0.0.1;Port=3306;Database=hiraya_learning_center;User=root;Password=";
        var options = new DbContextOptionsBuilder<HirayaLearningCenterDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 21)))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new HirayaLearningCenterDbContext(options);
    }
}
