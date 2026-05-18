using System;
using System.Threading.Tasks;
using Host.Settings;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Host.App.Backend
{
    /// Thin REST client for the Python backend (FastAPI, default port 8000).
    /// Endpoints mirrored: /api/health, /api/sessions, /api/sessions/{id},
    /// /api/sessions/{id}/events, /api/sessions/{id}/summary.
    public sealed class BackendClient
    {
        readonly string _baseUrl;

        public BackendClient(AppSettings settings) :
            this(settings?.RestUrl ?? "http://127.0.0.1:8000") { }

        public BackendClient(string baseUrl)
        {
            _baseUrl = baseUrl?.TrimEnd('/') ?? "";
        }

        public Task<bool> HealthAsync() =>
            GetAsync<HealthDto>("/api/health").ContinueWith(t => t.Result?.status == "ok");

        public Task<SessionListDto> ListSessionsAsync() =>
            GetAsync<SessionListDto>("/api/sessions");

        public Task<SessionSummaryDto> GetSummaryAsync(string sessionId) =>
            GetAsync<SessionSummaryDto>($"/api/sessions/{sessionId}/summary");

        Task<T> GetAsync<T>(string path) where T : class
        {
            var tcs = new TaskCompletionSource<T>();
            var url = _baseUrl + path;
            var req = UnityWebRequest.Get(url);
            req.timeout = 10;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[Backend] GET {url} → {req.result}: {req.error}");
                        tcs.SetResult(null);
                        return;
                    }
                    var json = req.downloadHandler.text;
                    var dto = JsonConvert.DeserializeObject<T>(json);
                    tcs.SetResult(dto);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Backend] parse {url}: {e.Message}");
                    tcs.SetResult(null);
                }
                finally
                {
                    req.Dispose();
                }
            };
            return tcs.Task;
        }

        [System.Serializable] class HealthDto { public string status; }

        public sealed class SessionListDto
        {
            [JsonProperty("sessions")] public string[] Sessions;
        }
    }
}
