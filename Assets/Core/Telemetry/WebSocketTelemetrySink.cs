using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Host.Telemetry
{
    public sealed class WebSocketTelemetrySink : ITelemetrySink
    {
        const int QueueCapacity = 4096;

        static readonly JsonSerializerSettings JsonSettings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        readonly ConcurrentQueue<TelemetryEvent> _queue = new();
        readonly SemaphoreSlim _signal = new(0);
        int _queueCount;
        int _droppedCount;

        ClientWebSocket _ws;
        CancellationTokenSource _pumpCts;
        Task _pumpTask;
        Task _receiveTask;

        public bool IsConnected => _ws?.State == WebSocketState.Open;
        public int DroppedEventCount => _droppedCount;

        public async Task ConnectAsync(Uri url, CancellationToken ct)
        {
            if (_ws != null) throw new InvalidOperationException("Sink already connected.");
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(url, ct).ConfigureAwait(false);
            _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _pumpTask = Task.Run(() => PumpLoop(_pumpCts.Token));
            _receiveTask = Task.Run(() => ReceiveLoop(_pumpCts.Token));
        }

        public void Enqueue(TelemetryEvent evt)
        {
            if (evt == null) return;
            if (Interlocked.Increment(ref _queueCount) > QueueCapacity)
            {
                Interlocked.Decrement(ref _queueCount);
                Interlocked.Increment(ref _droppedCount);
                return;
            }
            _queue.Enqueue(evt);
            try { _signal.Release(); }
            catch (ObjectDisposedException) { /* shutting down */ }
            catch (SemaphoreFullException) { /* extremely unlikely; drop silently */ }
        }

        async Task PumpLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _signal.WaitAsync(ct).ConfigureAwait(false);
                    if (!_queue.TryDequeue(out var evt)) continue;
                    Interlocked.Decrement(ref _queueCount);

                    var json = JsonConvert.SerializeObject(evt, JsonSettings);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await _ws.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] pump terminated: {e.Message}");
            }
        }

        async Task ReceiveLoop(CancellationToken ct)
        {
            var buf = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct)
                        .ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Swallow on shutdown; pump-loop log surfaces fatal errors.
            }
        }

        public async Task DisconnectAsync(CancellationToken ct)
        {
            _pumpCts?.Cancel();

            try
            {
                if (_ws?.State == WebSocketState.Open)
                {
                    await _ws.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "session_end",
                        ct).ConfigureAwait(false);
                }
            }
            catch
            {
                // Ignored: best-effort close.
            }

            if (_pumpTask != null) { try { await _pumpTask.ConfigureAwait(false); } catch { } }
            if (_receiveTask != null) { try { await _receiveTask.ConfigureAwait(false); } catch { } }

            _ws?.Dispose();
            _pumpCts?.Dispose();
            _signal.Dispose();
            _ws = null;
            _pumpCts = null;
            _pumpTask = null;
            _receiveTask = null;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
