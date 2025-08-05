using System;
using Xunit;
using backend.Controllers;
using backend.DbModel;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests
{
    public class WorkHoursControllerTests
    {
        [Fact]
        public void CrudTest_DoesNotThrow()
        {
            Exception? ex = Record.Exception(() => WorkHoursController.CrudTest());
            Assert.Null(ex);
        }

        [Fact]
        public void GetProductWorkStatus_ReturnsNotFound_WhenSerialNoDoesNotExist()
        {
            var controller = new WorkHoursController();
            var result = controller.GetProductWorkStatus("NON_EXISTENT_SN");
            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public void GetAllProductStates_ReturnsOkResult()
        {
            var controller = new WorkHoursController();
            var result = controller.GetAllProductStates();
            Assert.IsType<OkObjectResult>(result);
        }
    }
}
