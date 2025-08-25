using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using OT.Assessment.Consumer.Models;
using OT.Assessment.Consumer.Persistence;

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

    public async Task InsertBulkAsync(IReadOnlyList<CasinoWagerMessage> msgs, CancellationToken ct)
    {
        if (msgs.Count == 0) return;

        using var con = (SqlConnection)_factory.Create();
        using var cmd = new SqlCommand("casino.usp_IngestWagersBulk", con) { CommandType = CommandType.StoredProcedure };

        var tvp = new DataTable();
        tvp.Columns.Add("WagerId", typeof(Guid));
        tvp.Columns.Add("AccountId", typeof(Guid));
        tvp.Columns.Add("Username", typeof(string));
        tvp.Columns.Add("GameName", typeof(string));
        tvp.Columns.Add("Provider", typeof(string));
        tvp.Columns.Add("Amount", typeof(decimal));
        tvp.Columns.Add("CreatedDateTime", typeof(DateTimeOffset));
        foreach (var m in msgs)
            tvp.Rows.Add(m.WagerId, m.AccountId, m.Username, m.GameName, m.Provider, m.Amount, m.CreatedDateTime);

        var p = cmd.Parameters.AddWithValue("@Items", tvp);
        p.SqlDbType = SqlDbType.Structured;
        p.TypeName = "casino.WagerIngestType";

        await con.OpenAsync(ct);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
