using System;
using Xunit;
using backend.Controllers;
using backend.DbModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests
{
    public class WorkHoursControllerTests
    {
        [Fact]
        public void GetProductWorkStatus_ReturnsNotFound_WhenSerialNoDoesNotExist()
        {
            var controller = new WorkHoursController(NullLogger<WorkHoursController>.Instance);
            var result = controller.GetProductWorkStatus("NON_EXISTENT_SN");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void GetAllProductStates_ReturnsOkResult()
        {
            var controller = new WorkHoursController(NullLogger<WorkHoursController>.Instance);
            var result = controller.GetAllProductStates();
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
