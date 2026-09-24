# Contributing

## Branches

Cut a topic branch off `main`: `improve/<topic>`. Never commit directly to `main`.

## Commit messages

A single line: `<type>: <what changed>` (imperative mood, lowercase, no trailing period, at most 72
characters). Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `chore`.

## Tests

Run the full suite before opening a pull request:

```
dotnet test test/FlowValidate.Test/FlowValidate.Test.csproj
```

Existing tests are the compatibility contract: they must stay green and their assertions must not be
changed. Add new tests for new behaviour or to cover a fixed defect.

## Producing packages / release order

```
dotnet pack FlowValidate.sln -c Release -o <dir>
```

This produces exactly two packages: `FlowValidate` and `FlowValidate.AspNetCore`. When publishing,
push them in dependency order — `FlowValidate` first, then `FlowValidate.AspNetCore`.

`dotnet pack` runs the SDK's built-in package validation (`EnablePackageValidation`) against
`PackageValidationBaselineVersion` in each `.csproj`, catching public API breaks before they ship.
After every release, pull that baseline forward to the version that was just published.

## More

See the full development guide at
https://github.com/kadirdemirkaya/FlowValidate/blob/main/README.md
