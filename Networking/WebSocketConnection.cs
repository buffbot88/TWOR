using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace WorldOfRa.Server.Networking;

public sealed class WebSocketConnection
{
    private readonly WebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketConnection(string id, WebSocket socket)
    {
        Id = id;
        _socket = socket;
    }

    public string Id { get; }

    public WebSocketState State => _socket.State;

    public Task<bool> SendJsonAsync(object message, CancellationToken cancellationToken) =>
        SendJsonAsync(message, TimeSpan.FromSeconds(2), cancellationToken);

    public async Task<bool> SendJsonAsync(object message, TimeSpan sendTimeout, CancellationToken cancellationToken)
    {
        if (_socket.State != WebSocketState.Open)
        {
            return false;
        }

        var json = JsonSerializer.Serialize(message, JsonDefaults.SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(sendTimeout);

        var hasLock = false;
        try
        {
            await _sendLock.WaitAsync(timeout.Token);
            hasLock = true;

            if (_socket.State == WebSocketState.Open)
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "Send timeout", CancellationToken.None);
            return false;
        }
        finally
        {
            if (hasLock)
            {
                _sendLock.Release();
            }
        }
    }

    public async Task CloseAsync(WebSocketCloseStatus status, string reason, CancellationToken cancellationToken)
    {
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(status, reason, cancellationToken);
            }
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
