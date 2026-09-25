using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers.Wholesale
{
    public class DeliveriesController : ControllerBase
    {
        [HttpPost("wholesale/deliveries/create")]
        public IActionResult Create([FromBody] Parcel parcel)
        {
            return Ok(new { Accepted = parcel.Code });
        }
    }
}
