using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    public class OrdersController : ControllerBase
    {
        [HttpPost("orders/create")]
        public IActionResult Create([FromBody] Order order)
        {
            return Ok(new { Accepted = order.Name });
        }
    }
}
