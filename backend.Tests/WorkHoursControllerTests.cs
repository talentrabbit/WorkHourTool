using System;
using Xunit;
using backend.Controllers;
using backend.DbModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using backend.Data;
using backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests
{
    public class WorkHoursControllerTests : IDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly ServiceProvider _services;
        private readonly AppDbContext _db;
        private readonly WorkSessionManager _manager;

        public WorkHoursControllerTests()
        {
            _connection.Open();
            _services = new ServiceCollection()
                .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection))
                .BuildServiceProvider();
            _db = _services.GetRequiredService<AppDbContext>();
            _manager = new WorkSessionManager(NullLogger<WorkSessionManager>.Instance,
                _services.GetRequiredService<IServiceScopeFactory>());
        }

        public void Dispose()
        {
            _manager.Dispose();
            _services.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public void GetProductWorkStatus_ReturnsNotFound_WhenSerialNoDoesNotExist()
        {
            var controller = new WorkHoursController(NullLogger<WorkHoursController>.Instance, _manager, _db);
            var result = controller.GetProductWorkStatus("NON_EXISTENT_SN");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void GetAllProductStates_ReturnsOkResult()
        {
            var controller = new WorkHoursController(NullLogger<WorkHoursController>.Instance, _manager, _db);
            var result = controller.GetAllProductStates();
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
