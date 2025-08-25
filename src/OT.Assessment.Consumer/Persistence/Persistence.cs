using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using OT.Assessment.Consumer.Models;

namespace OT.Assessment.Consumer.Persistence;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _cs;
    public SqlServerConnectionFactory(string connectionString) => _cs = connectionString;
    public IDbConnection Create() => new SqlConnection(_cs);
}

public interface IWagerWriter
{
    Task InsertAsync(CasinoWagerMessage msg, CancellationToken ct);
}

public sealed class WagerWriter : IWagerWriter
{
    private readonly IDbConnectionFactory _factory;
    public WagerWriter(IDbConnectionFactory factory) => _factory = factory;

    public async Task InsertAsync(CasinoWagerMessage msg, CancellationToken ct)
    {
        using var con = _factory.Create();
        var p = new DynamicParameters();
        p.Add("@WagerId", msg.WagerId);
        p.Add("@AccountId", msg.AccountId);
        p.Add("@Username", msg.Username);
        p.Add("@GameName", msg.GameName);
        p.Add("@Provider", msg.Provider);
        p.Add("@Amount", msg.Amount);
        p.Add("@CreatedDateTime", msg.CreatedDateTime);

        await con.ExecuteAsync("casino.usp_IngestWager", p, commandType: CommandType.StoredProcedure);
    }
}
