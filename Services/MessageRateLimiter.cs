using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace WorldOfRa.Server.Services;

public sealed class MessageRateLimiter
{
    private readonly ConcurrentDictionary<string, FixedWindowCounter> _moveCounters = new();
    private readonly WorldSocketOptions _options;

    public MessageRateLimiter(IOptions<WorldSocketOptions> options)
    {
        _options = options.Value;
    }

    public bool TryConsumeMove(string connectionId)
    {
        var counter = _moveCounters.GetOrAdd(connectionId, _ => new FixedWindowCounter());
        return counter.TryConsume(Math.Max(1, _options.MaxMoveMessagesPerSecond), TimeSpan.FromSeconds(1));
    }

    public void Remove(string connectionId)
    {
        _moveCounters.TryRemove(connectionId, out _);
    }

    private sealed class FixedWindowCounter
    {
        private readonly object _gate = new();
        private DateTimeOffset _windowStartedUtc = DateTimeOffset.UtcNow;
        private int _count;

        public bool TryConsume(int maxCount, TimeSpan window)
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;

                if (now - _windowStartedUtc >= window)
                {
                    _windowStartedUtc = now;
                    _count = 0;
                }

                if (_count >= maxCount)
                {
                    return false;
                }

                _count++;
                return true;
            }
        }
    }
}
