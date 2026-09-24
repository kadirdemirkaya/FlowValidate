using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class BasketValidator : BaseValidator<Basket>
    {
        public BasketValidator()
        {
            ValidateCollection(x => x.Lines, new BasketLineValidator(), item => item)
                .WithIndexedPropertyNames("Lines");
        }
    }
}
