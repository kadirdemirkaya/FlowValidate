using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    public class ShipmentsController : ControllerBase
    {
        [HttpPost("shipments/save")]
        public async Task<IActionResult> SaveAsync([FromBody] Order order)
        {
            await Task.Yield();

            return Ok(new { Accepted = order.Name });
        }

        [HttpPost("shipments/submit")]
        [ActionName("Submit")]
        public IActionResult Post([FromBody] Order order)
        {
            return Ok(new { Accepted = order.Name });
        }

        [HttpPost("shipments/draft")]
        public async Task<IActionResult> DraftAsync(Order order)
        {
            await Task.Yield();

            return Ok(new { Accepted = order.Name });
        }
    }
}
