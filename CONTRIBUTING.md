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

## More

See the full development guide at
https://github.com/kadirdemirkaya/FlowValidate/blob/main/README.md
