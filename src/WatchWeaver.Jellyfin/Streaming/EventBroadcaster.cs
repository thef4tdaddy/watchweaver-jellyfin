using System.Collections.Concurrent;
using System.Threading.Channels;
using WatchWeaver.Jellyfin.Protocol;

namespace WatchWeaver.Jellyfin.Streaming;

public sealed class EventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<EventEnvelope>> _subscribers = new();
    private readonly Queue<EventEnvelope> _replay = new();
    private readonly object _gate = new();
    private const int ReplayCapacity = 256;

    public (Guid Id, ChannelReader<EventEnvelope> Reader) Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateBounded<EventEnvelope>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        });
        lock (_gate)
        {
            foreach (var value in _replay) channel.Writer.TryWrite(value);
        }
        _subscribers[id] = channel;
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel)) channel.Writer.TryComplete();
    }

    public int Publish(EventEnvelope value)
    {
        lock (_gate)
        {
            _replay.Enqueue(value);
            while (_replay.Count > ReplayCapacity) _replay.Dequeue();
        }
        var delivered = 0;
        foreach (var subscriber in _subscribers)
        {
            if (subscriber.Value.Writer.TryWrite(value)) delivered++;
            else Unsubscribe(subscriber.Key);
        }
        return delivered;
    }

    public int SubscriberCount => _subscribers.Count;
}
