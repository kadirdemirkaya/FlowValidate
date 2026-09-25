using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    [ApiController]
    public class InvoicesController : ControllerBase
    {
        [HttpPost("invoices/create")]
        public IActionResult Create([FromBody] Invoice invoice)
        {
            return Ok(new { Accepted = invoice.CustomerName });
        }
    }
}
