using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SignDocumentService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    
    public class PruebasController : ControllerBase
    {
        private readonly ILogger<PruebasController> _logger;
        public PruebasController(ILogger<PruebasController> logger)
        {
            _logger = logger;
        }
        [HttpGet("ping")]
        [AllowAnonymous]
        public IActionResult Ping()
        {
            return Ok("pong");
        }
    }
}
