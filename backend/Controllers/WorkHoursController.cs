using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkHoursController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromBody] WorkHourDto dto)
        {
            // Save dto to database (not implemented here)
            return Ok(new { message = "Work hours received", data = dto });
        }
    }

    public class WorkHourDto
    {
        public string WorkerName { get; set; }
        public int MainTime { get; set; }
        public int IssueTime { get; set; }
    }
}
