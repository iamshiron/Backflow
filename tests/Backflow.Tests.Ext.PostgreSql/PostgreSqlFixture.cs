using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Shiron.Backflow.Tests.Ext.PostgreSql;

public sealed class PostgreSqlFixture : IAsyncLifetime {
    private PostgreSqlContainer? _container;
    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public string ConnectionString => _container!.GetConnectionString();
    public async Task InitializeAsync() {
        if (Environment.GetEnvironmentVariable("BACKFLOW_POSTGRES_TESTS") != "1") return;
        _container = new PostgreSqlBuilder("postgres:18-alpine").WithPassword(Guid.NewGuid().ToString("N")).Build();
        await _container.StartAsync();
        DataSource = NpgsqlDataSource.Create(ConnectionString);
    }
    public async Task DisposeAsync() {
        if (_container is null) return;
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}
