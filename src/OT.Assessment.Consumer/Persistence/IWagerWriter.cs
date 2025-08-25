using OT.Assessment.Consumer.Models;

public interface IWagerWriter
{
    Task InsertAsync(CasinoWagerMessage msg, CancellationToken ct);
    Task InsertBulkAsync(IReadOnlyList<CasinoWagerMessage> msgs, CancellationToken ct);
}
