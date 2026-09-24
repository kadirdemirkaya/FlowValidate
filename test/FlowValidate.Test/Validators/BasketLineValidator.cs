using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class BasketLineValidator : BaseValidator<BasketLine>
    {
        public BasketLineValidator()
        {
            RuleFor(x => x.Sku).IsNotEmpty().WithMessage("Sku is required.", "BASKET_LINE_SKU_REQUIRED");
        }
    }
}
