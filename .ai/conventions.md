# Conventions and working rules

## Language

- Respond to the maintainer in Japanese (their working language). Code identifiers are in English.
- This context and the README are written in English for the public repository. `docs/plan.md` is the Japanese specification.
- For code comments, match the language of the surrounding code.

## Coding conventions

Follow the Microsoft C# coding conventions (not strictly).

- Line endings are CRLF in the working tree (`.gitattributes` / `.editorconfig`); Git stores LF. If a tool writes LF, that is fine: Git normalizes it on commit.
- Indent with 4 spaces. Use Allman braces.
- `PascalCase` for types, methods, properties, and public members. `camelCase` for locals and parameters.
- Private fields are `_camelCase`, interfaces start with `I`, async methods end with `Async`.
- `var` is fine when the type is obvious. Use keyword forms for built-in types (`int`, `string`).
- File-scoped namespaces (`namespace Trimx.Core;`).
- Put `using` directives at the top and remove unused ones.
- One type per file, with the file name matching the type name.
- Respect nullable reference types and avoid the `!` suppression operator.
- Always dispose `IDisposable` instances reliably (`using`).
- Avoid swallowing exceptions. Catch only where failure is acceptable (such as the clipboard), and comment the intent.
- Comments explain "why". Add `///` XML docs to public APIs where useful.
- In XAML, put one attribute per line when there are many. Keep code-behind minimal (put logic in Core / Services).

## Working rules

- When unsure about behavior, check `docs/plan.md`. **Do not add features that are not in plan.md.** If you change the specification, update plan.md too.
- After changes, confirm `dotnet build Trimx.slnx` succeeds.
- When adding a NuGet package, state the reason (the app is kept lightweight on purpose), and update `THIRD-PARTY-NOTICES.md`.
- When you implement or change a feature, update the relevant part of `.ai/implementation-notes.md`.
- When you change `.ai/`, edit only `.ai/`; do not copy content into the entry-point files (`AGENTS.md`, etc.).
