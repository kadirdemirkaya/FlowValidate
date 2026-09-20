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

        [HttpPost("orders/search")]
        public IActionResult Search([FromBody] Order order, [FromQuery] OrderFilter filter)
        {
            return Ok(new { Accepted = order.Name, Term = filter.Term });
        }

        [HttpPost("orders/page")]
        public IActionResult Page([FromBody] Order order, [FromQuery] int page)
        {
            return Ok(new { Accepted = order.Name, Page = page });
        }

        [HttpPost("orders/draft")]
        public IActionResult Draft(Order order)
        {
            return Ok(new { Accepted = order.Name });
        }

        [HttpPost("orders/lookup")]
        public IActionResult Lookup([FromQuery] string term)
        {
            return Ok(new { Term = term });
        }
    }
}
