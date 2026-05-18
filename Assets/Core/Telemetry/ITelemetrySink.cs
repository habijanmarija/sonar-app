using System;
using System.Threading;
using System.Threading.Tasks;

namespace Host.Telemetry
{
    public interface ITelemetrySink : IAsyncDisposable
    {
        bool IsConnected { get; }
        int DroppedEventCount { get; }
        Task ConnectAsync(Uri url, CancellationToken ct);
        void Enqueue(TelemetryEvent evt);
        Task DisconnectAsync(CancellationToken ct);
    }
}
