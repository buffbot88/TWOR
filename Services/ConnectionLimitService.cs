namespace WorldOfRa.Server.Services;

public sealed class ConnectionLimitService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _connectionsByIp = new(StringComparer.OrdinalIgnoreCase);
    private readonly WorldSocketOptions _options;
    private int _connectionCount;

    public ConnectionLimitService(Microsoft.Extensions.Options.IOptions<WorldSocketOptions> options)
    {
        _options = options.Value;
    }

    public int ActiveConnectionCount
    {
        get
        {
            lock (_gate)
            {
                return _connectionCount;
            }
        }
    }

    public bool TryAcquire(string remoteIp, out ConnectionLease? lease, out string reason)
    {
        lock (_gate)
        {
            if (_connectionCount >= Math.Max(1, _options.MaxConnections))
            {
                lease = null;
                reason = "Global connection limit reached.";
                return false;
            }

            _connectionsByIp.TryGetValue(remoteIp, out var ipCount);
            if (ipCount >= Math.Max(1, _options.MaxConnectionsPerIp))
            {
                lease = null;
                reason = "Per-IP connection limit reached.";
                return false;
            }

            _connectionCount++;
            _connectionsByIp[remoteIp] = ipCount + 1;
            lease = new ConnectionLease(this, remoteIp);
            reason = string.Empty;
            return true;
        }
    }

    private void Release(string remoteIp)
    {
        lock (_gate)
        {
            _connectionCount = Math.Max(0, _connectionCount - 1);

            if (!_connectionsByIp.TryGetValue(remoteIp, out var ipCount))
            {
                return;
            }

            if (ipCount <= 1)
            {
                _connectionsByIp.Remove(remoteIp);
            }
            else
            {
                _connectionsByIp[remoteIp] = ipCount - 1;
            }
        }
    }

    public sealed class ConnectionLease : IDisposable
    {
        private readonly ConnectionLimitService _owner;
        private readonly string _remoteIp;
        private int _disposed;

        internal ConnectionLease(ConnectionLimitService owner, string remoteIp)
        {
            _owner = owner;
            _remoteIp = remoteIp;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _owner.Release(_remoteIp);
            }
        }
    }
}
