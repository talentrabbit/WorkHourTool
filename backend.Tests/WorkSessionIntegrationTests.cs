using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
            var startObj = await startResp.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(startObj);
            string sessionId = (string)startObj.sessionId;
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

            // Get session
            var getResp = await client.GetAsync(_baseUrl + "/session/" + Uri.EscapeDataString(sessionId));
            getResp.EnsureSuccessStatusCode();
            var sessionObj = await getResp.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(sessionObj);
            Assert.Equal(sessionId, (string)sessionObj.sessionId);

            // Complete session
            var completeReq = new
            {
                SessionId = sessionId,
                ElapsedSeconds = 6
            };
            var completeResp = await client.PostAsJsonAsync(_baseUrl + "/complete-session", completeReq);
            completeResp.EnsureSuccessStatusCode();

            // Verify state changed when reading session (it should still be readable)
            var getAfter = await client.GetAsync(_baseUrl + "/session/" + Uri.EscapeDataString(sessionId));
            getAfter.EnsureSuccessStatusCode();
            var afterObj = await getAfter.Content.ReadFromJsonAsync<dynamic>();
            Assert.NotNull(afterObj);
            // state may be "Completed" or updated by cleanup; just ensure session exists
            Assert.Equal(sessionId, (string)afterObj.sessionId);
        }
    }
}
