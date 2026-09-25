using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers.Retail
{
    public class DeliveriesController : ControllerBase
    {
        [HttpPost("retail/deliveries/create")]
        public IActionResult Create([FromBody] Order order)
        {
            return Ok(new { Accepted = order.Name });
        }
    }
}
