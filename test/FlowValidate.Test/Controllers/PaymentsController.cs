using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        [HttpPost("payments/create")]
        public IActionResult Create([FromBody] Order order)
        {
            return Ok(new { Accepted = order.Name });
        }
    }
}
