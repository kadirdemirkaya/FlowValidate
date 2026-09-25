namespace FlowValidate.Test
{
    public class StopOnFirstFailureTest
    {
        private class Sample
        {
            public string? Email { get; set; }
            public string? Name { get; set; }
        }

        private class DefaultBehaviorValidator : BaseValidator<Sample>
        {
            public DefaultBehaviorValidator()
            {
                UseDescriptiveMessages();
                RuleFor(x => x.Email).IsNotEmpty().IsEmail();
            }
        }

        private class ChainOptInValidator : BaseValidator<Sample>
        {
            public ChainOptInValidator()
            {
                UseDescriptiveMessages();
                RuleFor(x => x.Email).IsNotEmpty().IsEmail().StopOnFirstFailure();
            }
        }

        private class ChainOptInIgnoresPlacementValidator : BaseValidator<Sample>
        {
            public ChainOptInIgnoresPlacementValidator()
            {
                UseDescriptiveMessages();
                RuleFor(x => x.Email).StopOnFirstFailure().IsNotEmpty().IsEmail();
            }
        }

        private class ValidatorDefaultValidator : BaseValidator<Sample>
        {
            public ValidatorDefaultValidator()
            {
                UseDescriptiveMessages();
                UseStopOnFirstFailure();

                RuleFor(x => x.Email).IsNotEmpty().IsEmail();
                RuleFor(x => x.Name).IsNotEmpty().Length(3, 10);
            }
        }

        private class ChainOverridesValidatorDefaultValidator : BaseValidator<Sample>
        {
            public ChainOverridesValidatorDefaultValidator()
            {
                UseDescriptiveMessages();
                UseStopOnFirstFailure();

                RuleFor(x => x.Email).IsNotEmpty().IsEmail().StopOnFirstFailure(false);
            }
        }

        private static int _calls;

        private class AsyncTrackingValidator : BaseValidator<Sample>
        {
            public AsyncTrackingValidator()
            {
                UseDescriptiveMessages();
                RuleFor(x => x.Email)
                    .IsNotEmpty()
                    .MustAsync(value =>
                    {
                        _calls++;
                        return Task.FromResult(true);
                    })
                    .StopOnFirstFailure();
            }
        }

        [Fact]
        public void Default_RunsTheWholeChain_ReportingEveryFailure()
        {
            var validator = new DefaultBehaviorValidator();

            var result = validator.Validate(new Sample { Email = "" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, result.Failures[0].ErrorCode);
            Assert.Equal(BuiltInRuleCodes.Email, result.Failures[1].ErrorCode);
        }

        [Fact]
        public void ChainStopOnFirstFailure_StopsAfterTheFirstFailingRule()
        {
            var validator = new ChainOptInValidator();

            var result = validator.Validate(new Sample { Email = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, failure.ErrorCode);
        }

        [Fact]
        public void ChainStopOnFirstFailure_IsIndependentOfPlacementInTheChain()
        {
            var validator = new ChainOptInIgnoresPlacementValidator();

            var result = validator.Validate(new Sample { Email = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, failure.ErrorCode);
        }

        [Fact]
        public void ChainStopOnFirstFailure_DoesNotStopWhenTheFirstRulePasses()
        {
            var validator = new ChainOptInValidator();

            var result = validator.Validate(new Sample { Email = "not-an-email" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.Email, failure.ErrorCode);
        }

        [Fact]
        public void ValidatorDefault_AppliesToEveryChain()
        {
            var validator = new ValidatorDefaultValidator();

            var result = validator.Validate(new Sample { Email = "", Name = "" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, result.Failures[0].ErrorCode);
            Assert.Equal("Email", result.Failures[0].PropertyName);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, result.Failures[1].ErrorCode);
            Assert.Equal("Name", result.Failures[1].PropertyName);
        }

        [Fact]
        public void ChainStopOnFirstFailureFalse_OverridesTheValidatorDefault()
        {
            var validator = new ChainOverridesValidatorDefaultValidator();

            var result = validator.Validate(new Sample { Email = "" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, result.Failures[0].ErrorCode);
            Assert.Equal(BuiltInRuleCodes.Email, result.Failures[1].ErrorCode);
        }

        [Fact]
        public async Task StopOnFirstFailure_DoesNotAwaitTheAsyncRuleAfterAFailure()
        {
            _calls = 0;
            var validator = new AsyncTrackingValidator();

            var result = await validator.ValidateAsync(new Sample { Email = "" });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.NotEmpty, failure.ErrorCode);
            Assert.Equal(0, _calls);
        }

        private class ShouldMultiErrorValidator : BaseValidator<Sample>
        {
            public ShouldMultiErrorValidator()
            {
                RuleFor(x => x.Email)
                    .Should((value, error) =>
                    {
                        error("First should failure.");
                        error("Second should failure.");
                    })
                    .IsEmail()
                    .StopOnFirstFailure();
            }
        }

        [Fact]
        public void StopOnFirstFailure_ReportsEveryErrorFromAFailingShouldRule_ThenStops()
        {
            var validator = new ShouldMultiErrorValidator();

            var result = validator.Validate(new Sample { Email = "not-an-email" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("First should failure.", result.Failures[0].ErrorMessage);
            Assert.Equal("Second should failure.", result.Failures[1].ErrorMessage);
        }

        private class ShouldAsyncMultiErrorValidator : BaseValidator<Sample>
        {
            public ShouldAsyncMultiErrorValidator()
            {
                RuleFor(x => x.Email)
                    .ShouldAsync(async (value, error) =>
                    {
                        await Task.Yield();
                        error("First should failure.");
                        error("Second should failure.");
                    })
                    .MustAsync(value =>
                    {
                        _calls++;
                        return Task.FromResult(true);
                    })
                    .StopOnFirstFailure();
            }
        }

        [Fact]
        public async Task StopOnFirstFailure_ReportsEveryErrorFromAFailingShouldAsyncRule_ThenStopsWithoutAwaitingTheNextRule()
        {
            _calls = 0;
            var validator = new ShouldAsyncMultiErrorValidator();

            var result = await validator.ValidateAsync(new Sample { Email = "anything" });

            Assert.Equal(2, result.Failures.Count);
            Assert.Equal("First should failure.", result.Failures[0].ErrorMessage);
            Assert.Equal("Second should failure.", result.Failures[1].ErrorMessage);
            Assert.Equal(0, _calls);
        }

        private class RequiredIfValidator : BaseValidator<Sample>
        {
            public RequiredIfValidator(bool stopOnFirstFailure)
            {
                var chain = RuleFor(x => x.Name).RequiredIf(value => value != null).Length(3, 10);

                if (stopOnFirstFailure)
                    chain.StopOnFirstFailure();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RequiredIf_StillSkipsRemainingRules_RegardlessOfStopOnFirstFailure(bool stopOnFirstFailure)
        {
            var validator = new RequiredIfValidator(stopOnFirstFailure);

            var result = validator.Validate(new Sample { Name = null });

            var failure = Assert.Single(result.Failures);
            Assert.Equal(BuiltInRuleCodes.Required, failure.ErrorCode);
        }
    }
}
