using Xunit;

namespace Shiron.Backflow.Tests.Ext.PostgreSql;

public sealed class PostgreSqlFactAttribute : FactAttribute {
    public PostgreSqlFactAttribute() {
        if (Environment.GetEnvironmentVariable("BACKFLOW_POSTGRES_TESTS") != "1")
            Skip = "Opt-in isolated PostgreSQL checks require BACKFLOW_POSTGRES_TESTS=1 and a Docker-compatible engine.";
    }
}
