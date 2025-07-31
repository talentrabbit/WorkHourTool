using System;
using Xunit;
using backend.Controllers;
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
    }
}
