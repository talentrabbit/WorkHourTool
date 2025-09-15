using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using System;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace backend.Tests
{
    public class TestServerFactory : WebApplicationFactory<Program>, IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly string _connectionString;
        public TestServerFactory()
        {
            // Ensure the app under test doesn't register Negotiate during tests
            Environment.SetEnvironmentVariable("TEST_DISABLE_NEGOTIATE", "1");

            // Use an in-memory SQLite database for tests
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _connectionString = _connection.ConnectionString;
        }

        // ConfigureWebHost runs earlier than CreateHost and allows us to replace services
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Replace AppDbContext with one that uses the in-memory SQLite
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Remove the cleanup hosted service to avoid interference
                var hosted = services.SingleOrDefault(d => d.ImplementationType == typeof(backend.Services.WorkSessionCleanupService));
                if (hosted != null) services.Remove(hosted);

                // Remove any Negotiate-related registrations and authentication scheme providers so we can force Test auth
                var negotiateDescriptors = services.Where(d => (d.ServiceType?.FullName?.Contains("Negotiate") ?? false) || (d.ImplementationType?.FullName?.Contains("Negotiate") ?? false)).ToList();
                foreach (var d in negotiateDescriptors) services.Remove(d);

                services.RemoveAll<IAuthenticationSchemeProvider>();
                services.RemoveAll<IAuthenticationHandlerProvider>();
                services.RemoveAll<IAuthenticationService>();

                // Replace Negotiate authentication with a test auth handler that always succeeds
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                // Ensure the Test scheme is the default for authentication/challenge in the test host
                services.PostConfigure<AuthenticationOptions>(opts =>
                {
                    opts.DefaultAuthenticateScheme = "Test";
                    opts.DefaultChallengeScheme = "Test";
                    opts.DefaultScheme = "Test";
                });
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            // Apply migrations on startup to create schema in the in-memory DB
            using (var scope = host.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
            }

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                try { _connection?.Close(); } catch { }
                try { _connection?.Dispose(); } catch { }
            }
        }
    }
}
