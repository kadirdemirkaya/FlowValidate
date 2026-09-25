using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class InvoiceValidator : BaseValidator<Invoice>
    {
        public InvoiceValidator()
        {
            RuleFor(x => x.CustomerName)
                .IsNotEmpty()
                .WithMessage("Customer name is required.", "INVOICE_CUSTOMER_REQUIRED");
        }
    }
}
