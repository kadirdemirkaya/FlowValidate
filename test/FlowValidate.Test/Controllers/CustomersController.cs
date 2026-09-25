using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    public class CustomersController : ControllerBase
    {
        [HttpPost("customers/create")]
        public IActionResult Create([FromBody] Customer customer)
        {
            return Ok(new { Accepted = customer.Billing?.City });
        }
    }
}
