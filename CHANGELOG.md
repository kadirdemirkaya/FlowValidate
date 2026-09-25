# Changelog

All notable changes to this project are documented in this file, following the
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format. Dates are
commit dates (UTC, `YYYY-MM-DD`). Entries whose version or date could not be
determined with confidence are marked ❓ instead of being guessed.

## [Unreleased]

## [1.5.0] — 2026-09-25

### Added
- `CancellationToken` support: `ValidateAsync(T instance, CancellationToken cancellationToken)` on `BaseValidator<T>` and on `IBaseValidator<T>` (as a default interface method, so hand-written implementations keep compiling), plus `MustAsync(Func<TProperty, CancellationToken, Task<bool>>)`, `ShouldAsync(Func<TProperty, Action<string>, CancellationToken, Task>)` and `ShouldAsync(Func<TProperty, CancellationToken, Task>, string?)`. The token reaches nested, collection and registry validators and is observed between rules. A cancelled token throws `OperationCanceledException` out of `ValidateAsync` instead of being reported as a validation failure. The existing signatures and their behavior are unchanged; validating without a token is exactly the same as passing `CancellationToken.None`.
- `UseFlowValidation()` now passes `HttpContext.RequestAborted` to the validator, so validation stops when the client disconnects. The obsolete `FlowValidationApp()` middleware stays frozen and validates without a token.
- Reflection note: `ValidateAsync` can no longer be resolved by name alone (`typeof(IBaseValidator<T>).GetMethod("ValidateAsync")` throws `AmbiguousMatchException`); both middlewares now select the overload by signature, and consumer code that reflects over the method must do the same.
- `MatchesRegex(string pattern, TimeSpan matchTimeout)` and `MatchesRegex(Regex regex)`: opt-in bounded regular expression matching for values that come from untrusted input, so a catastrophically backtracking pattern cannot occupy the thread. A timed-out match is reported as a rule failure with the error code `RegexTimeout` instead of throwing `RegexMatchTimeoutException` out of validation. The existing `MatchesRegex(string)` overload keeps its behavior and still matches without a time limit.
- `UseFlowValidation(Action<FlowValidationOptions>)` and `FlowValidationOptions.UseSystemTextJson` in `FlowValidate.AspNetCore`: opt in to deserializing the request body with `System.Text.Json` and the host's own `Microsoft.AspNetCore.Mvc.JsonOptions` settings (the ASP.NET Core web defaults when MVC is not registered), so a model relying on `[JsonPropertyName]`, a naming policy or a custom converter is validated as model binding builds it instead of being validated with unbound properties and answered with a wrong `400`. Off by default: the parameterless `UseFlowValidation()` keeps deserializing with `Newtonsoft.Json`. The response body of a failed validation, and leaving a malformed, empty or `null` body to the host's model binding, are unchanged with the option on.
- `UseDescriptiveMessages()` on `BaseValidator<T>` and `WithDescriptiveMessages()` on the `RuleFor` chain: opt in to per-rule failure messages and error codes for the built-in rules, so a failing rule reports the property and the bound it enforces (`"Name must be between 3 and 100 characters."`) with its own code (`Length`, `NotEmpty`, `InRange`, …) instead of `"Validation failed for property."` with `DefaultRule`. The codes are exposed as constants on the new `BuiltInRuleCodes` class. Off by default: without the call every message and code is exactly as before. `WithMessage` always wins — its message replaces the descriptive one, and its error code replaces the rule's code when one is passed. Custom rules (`Must`, `MustAsync`, `Should`, `ShouldAsync`), `RequiredIf` and the `RegexTimeout` failure are unaffected. The setting is per validator and is not propagated to the validators used by `ValidateNested`, `ValidateCollection` or `ValidateRegistryRules`; a single chain can opt in or out with `WithDescriptiveMessages(bool)`.
- `WithSeverity(Severity)` on the `RuleFor` chain: overrides the severity of the most recently added rule's failure, following the same "applies to the last rule, no-op before any rule" contract as `WithMessage`, and composable with it in either order. Has no effect on failures raised from inside a `Should`/`ShouldAsync` callback, matching `WithMessage`'s existing behavior there. The default `Severity.Error` and `ValidationResult.IsValid` semantics are unchanged.

## [1.4.0] — 2026-09-24

### Added
- `When(Func<T, bool>)` and `Unless(Func<T, bool>)` on the `RuleFor` chain: gate a property's whole rule chain behind a condition on the root instance, so it can branch on another property. An unmet condition skips the chain silently without reading the property and without producing a failure; async rules are gated the same way. `RequiredIf` is unchanged.
- `ValidationCollectionBuilder.WithIndexedPropertyNames(string)`: opt-in indexed property names for collection failures (`Items[1].Name`), so clients can tell which element failed without parsing the message. Off by default; property names and the `"Element n: "` message prefix are unchanged unless the method is called.
- `ValidationResult.ToDictionary()`: groups `Failures` by `PropertyName` into an `IDictionary<string, string[]>`, in insertion order, for building `ValidationProblemDetails`-style error bodies without a manual `GroupBy`.
- `FlowValidationAssemblies` in `FlowValidate.AspNetCore`: registers additional assemblies for `UseFlowValidation()` to scan for controllers, for apps whose controllers are spread across more than one assembly. Merges across repeated calls and multiple assemblies per call; registering the same assembly twice does not scan it twice.
- `net10.0` added as a target framework for `FlowValidate.AspNetCore`, matching the core package.
- XML documentation on the public API surface (`BaseValidator<T>`, `ValidationResult`, `ValidationFailure`, all `ValidationRuleBuilder<T,TProperty>` rule methods, `FlowValidationService`, `UseFlowValidation`) for IntelliSense, plus SourceLink and `.snupkg` symbol packages for both packages.

### Changed
- Stopped the two `src` projects from packing on every build (`GeneratePackageOnBuild`), so `dotnet pack` no longer races the build output and fails with `NU5026`; `FlowValidate.Console` is now excluded from solution-level packing.
- Pinned `Microsoft.Extensions.DependencyInjection.Abstractions` per target framework in the core package (`8.0.2` for net6.0/net7.0/net8.0, `9.0.20` for net9.0, `10.0.12` for net10.0) instead of one version across all TFMs, clearing the "doesn't support net6.0/net7.0" build warning.

### Fixed
- Stop `Should(Action<TProperty>, params string[])` from reporting its messages as failures when the action completes without throwing; messages are now only reported from the `catch` branch, as intended.
- Stop `Should`/`ShouldAsync` from reporting a second, generic `[DefaultRule]` failure alongside the real exception message when the callback throws.
- Fix `IsUnique()` always failing on value-typed collections (`List<int>`, `List<Guid>`) by checking against non-generic `IEnumerable` instead of the reference-type-only `IEnumerable<object>`; `List<string>` behavior and `null`-collection handling are unchanged.
- Stop the ASP.NET Core middleware from returning `500` for malformed JSON, an empty body, a `"null"` body, or a type-mismatched field; unparsable bodies are now left to the host's own model binding (its usual `400`) instead of throwing out of the middleware.
- Stop `IsGreaterThan(int)` and `IsLessThan(int)` from letting `OverflowException`, `FormatException`, and `InvalidCastException` escape `Convert.ToInt32` for out-of-range, non-numeric, or non-convertible values; unconvertible values are now reported as an ordinary rule failure. The existing decimal-truncation and `null`-as-zero behavior is unchanged; use the `IComparable` overloads for an exact comparison.
- Fix `ValidateCollection` throwing `NullReferenceException` on a `null` collection; it is now skipped, consistent with `ValidateNested`.
- Fix `Validate`/`ValidateAsync` throwing an unqualified `NullReferenceException` for a `null` root instance; they now throw `ArgumentNullException`.
- Keep `AttemptedValue` on failures produced through `WithMessage(...)` instead of reporting it as `null`.

### Security
- Pin `System.Text.Encodings.Web` to `8.0.0` in the core package to close a transitive Critical advisory (`GHSA-ghhp-997w-qr28`) pulled in through `Microsoft.AspNetCore.Http 2.1.34`, affecting both published packages.

## [1.3.0] — 2026-09-23

### Added
- New `FlowValidate.AspNetCore` package: `app.UseFlowValidation()` and a cached, assembly-scoped request-model validation middleware, replacing the core package's reflection-per-request middleware.
- `IComparable`-based overloads for `IsInRange`, `IsGreaterThan`, `IsLessThan`, alongside the existing `int`-based overloads (unchanged).

### Changed
- Cache controller/action → parameter-type lookups in the ASP.NET Core middleware instead of reflecting over the whole assembly on every request.

### Deprecated
- `FlowValidationApp()` and `ModelValidationMiddleware` in the core package are obsolete; both will be removed in the next major version. Migrate to `app.UseFlowValidation()` from the new `FlowValidate.AspNetCore` package — both middlewares already respond `400 Bad Request` on a validation failure, so migrating changes no status codes, only the registration call and the package it comes from.

### Fixed
- Fall back to the property expression's text when a rule's property expression isn't a simple member access, instead of returning `null` for `ValidationFailure.PropertyName`.
- Make `FlowValidationService` idempotent so registering validators from the same assembly more than once no longer duplicates DI registrations.
- Validate only the body-bound action parameter in the middleware, instead of re-reading the same request body for every parameter.

### Removed
- Dead internal state (`_shouldBuilder`, `_shouldListBuilder`, `GetAllErrors()`, `AnyListErrors`) that was never populated or read.

## [1.2.0] — 2026-05-21

- Added asynchronous validation rule support (`MustAsync`, `ShouldAsync`) and a synchronous `Validate()` bridge that throws when async rules are present.
- Cleared nullable/compiler warnings across `src`.

❓ Not verified locally whether this version was published to NuGet.

## [1.1.6] — 2025-09-23

- Added the `ValidationFailure` model (`PropertyName`, `AttemptedValue`, `ErrorMessage`, `ErrorCode`, `Severity`).

## [1.1.5] — 2025-09-23

- Updated test scenarios.

## [1.1.4] — 2025-09-22

- Version bump; no further detail recoverable from the commit message.

## [1.1.3] — 2025-09-21

- Initial published version.

[Unreleased]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.5.0...HEAD
[1.5.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.4.0...v1.5.0
[1.4.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.3.0...v1.4.0
[1.3.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.1.6...v1.2.0
[1.1.6]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.1.5...v1.1.6
