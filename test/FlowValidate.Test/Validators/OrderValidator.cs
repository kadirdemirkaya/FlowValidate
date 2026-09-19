using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class OrderValidator : BaseValidator<Order>
    {
        public OrderValidator()
        {
            RuleFor(x => x.Name).IsNotEmpty().WithMessage("Order name is required.", "ORDER_NAME_REQUIRED");
        }
    }
}
