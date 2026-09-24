using FlowValidate.Test.Models;
using Microsoft.AspNetCore.Mvc;

namespace FlowValidate.Test.Controllers
{
    public class TicketsController : ControllerBase
    {
        [HttpPost("tickets/create")]
        public IActionResult Create([FromBody] Ticket ticket)
        {
            return Ok(new
            {
                Accepted = ticket.Name,
                Observed = RequestCancellationProbe.Observed,
                CanBeCanceled = RequestCancellationProbe.ObservedToken.CanBeCanceled,
                MatchesRequestAborted = RequestCancellationProbe.ObservedToken.Equals(HttpContext.RequestAborted)
            });
        }
    }
}
