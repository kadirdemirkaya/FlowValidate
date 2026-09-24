using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    public class BasketsController : ControllerBase
    {
        [HttpPost("baskets/create")]
        public IActionResult Create([FromBody] Basket basket)
        {
            return Ok(new { Accepted = basket.Lines.Count });
        }
    }
}
