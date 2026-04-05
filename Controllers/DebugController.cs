using Microsoft.AspNetCore.Mvc;

namespace Mitrayana.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        [HttpGet("last-email")]
        public IActionResult LastEmail()
        {
            return NotFound(new { message = "Debug email preview endpoint has been removed. Configure a real SMTP server in 'Smtp:Host' to enable email delivery." });
        }
    }
}