using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class OrderFilterValidator : BaseValidator<OrderFilter>
    {
        public OrderFilterValidator()
        {
            RuleFor(x => x.Term).IsNotEmpty().WithMessage("Filter term is required.", "FILTER_TERM_REQUIRED");
        }
    }
}
