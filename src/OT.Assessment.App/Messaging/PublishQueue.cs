using System.Threading.Channels;
using OT.Assessment.App.Models;

namespace OT.Assessment.App.Messaging;

public interface IPublishQueue
{
    ValueTask EnqueueAsync(CasinoWagerMessage message, CancellationToken ct);
    ChannelReader<CasinoWagerMessage> Reader { get; }
}

public sealed class PublishQueue : IPublishQueue
{
    private readonly Channel<CasinoWagerMessage> _channel;

    public PublishQueue()
    {
        _channel = Channel.CreateBounded<CasinoWagerMessage>(
            new BoundedChannelOptions(capacity: 50000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
    }

    public ValueTask EnqueueAsync(CasinoWagerMessage message, CancellationToken ct)
        => _channel.Writer.WriteAsync(message, ct);

    public ChannelReader<CasinoWagerMessage> Reader => _channel.Reader;
}
