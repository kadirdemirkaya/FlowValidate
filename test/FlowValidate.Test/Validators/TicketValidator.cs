using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class TicketValidator : BaseValidator<Ticket>
    {
        public TicketValidator()
        {
            RuleFor(x => x.Name)
                .MustAsync((name, cancellationToken) =>
                {
                    RequestCancellationProbe.Observed = true;
                    RequestCancellationProbe.ObservedToken = cancellationToken;

                    return Task.FromResult(!string.IsNullOrWhiteSpace(name));
                })
                .WithMessage("Ticket name is required.", "TICKET_NAME_REQUIRED");
        }
    }
}
