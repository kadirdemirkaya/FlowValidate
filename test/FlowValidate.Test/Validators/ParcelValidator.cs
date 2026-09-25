using FlowValidate.Test.Models;

namespace FlowValidate.Test.Validators
{
    public class ParcelValidator : BaseValidator<Parcel>
    {
        public ParcelValidator()
        {
            RuleFor(x => x.Code).IsNotEmpty().WithMessage("Parcel code is required.", "PARCEL_CODE_REQUIRED");
        }
    }
}
