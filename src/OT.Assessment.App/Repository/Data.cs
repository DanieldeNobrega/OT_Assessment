using OT.Assessment.App.Models;

namespace OT.Assessment.App.Repository;
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

public interface IPlayerReadRepository
{
    Task<PaginatedResponse<PlayerWagerListItem>> GetPlayerWagersPagedAsync(Guid accountId, int page, int pageSize, CancellationToken ct);
    Task<IEnumerable<TopSpenderDto>> GetTopSpendersAsync(int count, CancellationToken ct);
}

public sealed class PlayerReadRepository : IPlayerReadRepository
{
    private readonly IDbConnectionFactory _factory;
    public PlayerReadRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<PaginatedResponse<PlayerWagerListItem>> GetPlayerWagersPagedAsync(Guid accountId, int page, int pageSize, CancellationToken ct)
    {
        using var con = _factory.Create();
        var parameters = new DynamicParameters();
        parameters.Add("@AccountId", accountId);
        parameters.Add("@PageNumber", page);
        parameters.Add("@PageSize", pageSize);

        using var multi = await con.QueryMultipleAsync(
            "casino.usp_GetPlayerWagersPaged",
            param: parameters,
            commandType: CommandType.StoredProcedure);

        var items = await multi.ReadAsync<PlayerWagerListItem>();
        var totals = await multi.ReadFirstAsync<(int Total, int TotalPages)>();
        return new PaginatedResponse<PlayerWagerListItem>
        {
            Data = items,
            Page = page,
            PageSize = pageSize,
            Total = totals.Total,
            TotalPages = totals.TotalPages
        };
    }

    public async Task<IEnumerable<TopSpenderDto>> GetTopSpendersAsync(int count, CancellationToken ct)
    {
        using var con = _factory.Create();
        return await con.QueryAsync<TopSpenderDto>(
            "casino.usp_GetTopSpenders",
            new { Top = count },
            commandType: CommandType.StoredProcedure);
    }
}