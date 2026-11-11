using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace backend.Tests
{
    public class WorkSessionIntegrationTests : IClassFixture<TestServerFactory>
    {
        private readonly TestServerFactory _factory;
        private readonly string _baseUrl = "/api/workhours";

        public WorkSessionIntegrationTests(TestServerFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task StartHeartbeatCompleteSession_EndToEnd()
        {
            using var client = _factory.CreateClient();

            // Start session
            var startReq = new
            {
                WorkHourId = (int?)null,
                WorkerName = "IntegrationTestUser",
                SerialNo = "SN-INTEGRATION-001",
                ProcessName = "Assembly",
                ElapsedSeconds = 0,
                ActiveClock = "active",
                MetadataJson = "{ \"ncmRows\": [] }",
                State = "Working"
            };

            var startResp = await client.PostAsJsonAsync(_baseUrl + "/start-session", startReq);
            startResp.EnsureSuccessStatusCode();
            var startObj = await startResp.Content.ReadFromJsonAsync<JsonElement>();
            string sessionId = startObj.GetProperty("sessionId").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(sessionId));

            // Send heartbeat
            var hbReq = new
            {
                SessionId = sessionId,
                ElapsedSeconds = 5,
                ActiveClock = "active",
                MetadataJson = "{ \"ncmCount\": 0 }",
                State = "Working"
            };

            var hbResp = await client.PostAsJsonAsync(_baseUrl + "/session-heartbeat", hbReq);
            hbResp.EnsureSuccessStatusCode();

            // There is no GET /session/{sessionId} route. Verify the live session through
            // the registered manager and persisted completion through the isolated SQLite DB.
            var manager = _factory.Services.GetRequiredService<WorkSessionManager>();
            var liveSession = manager.GetBySessionId(sessionId);
            Assert.NotNull(liveSession);
            Assert.Equal(sessionId, liveSession.SessionId);
            Assert.True(liveSession.ElapsedSeconds >= 5);

            // Complete session
            var completeReq = new
            {
                SessionId = sessionId,
                ElapsedSeconds = 6
            };
            var completeResp = await client.PostAsJsonAsync(_baseUrl + "/complete-session", completeReq);
            completeResp.EnsureSuccessStatusCode();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var completed = await db.WorkSessions.SingleAsync(s => s.SessionId == sessionId);
            Assert.Equal("Completed", completed.State);
            Assert.True(completed.ElapsedSeconds >= 6);
        }
    }
}
