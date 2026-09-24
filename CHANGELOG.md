# Changelog

All notable changes to this project are documented in this file, following the
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format. Dates are
commit dates (UTC, `YYYY-MM-DD`). Entries whose version or date could not be
determined with confidence are marked ❓ instead of being guessed.

## [Unreleased]

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

[Unreleased]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.1.6...v1.2.0
[1.1.6]: https://github.com/kadirdemirkaya/FlowValidate/compare/v1.1.5...v1.1.6
