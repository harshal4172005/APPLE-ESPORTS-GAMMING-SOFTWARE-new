namespace AppleEsportsErp.Application.DTOs.Common;

/// <summary>
/// Hardening C.1: Wraps all SignalR outgoing events to provide versioning and idempotency.
/// Prevents out-of-order execution on the frontend during reconnect storms.
/// </summary>
public class EventEnvelope<T>
{
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public int EventVersion { get; set; } = 1;
    public T Payload { get; set; }

    public EventEnvelope(T payload, int version = 1)
    {
        Payload = payload;
        EventVersion = version;
    }
}
