# Trimx — entry point for AI agents

A lightweight image editor for Windows 11 (C# / .NET 10 / WPF / SkiaSharp).

Shared context lives in `.ai/`. Read these before working:

1. `.ai/project.md` — overview, status, build, release
2. `.ai/architecture.md` — layering and design rules (e.g. never modify layers directly from the UI)
3. `.ai/conventions.md` — coding conventions and working rules (respond in Japanese, confirm the build, do not add features missing from plan.md)
4. `.ai/pitfalls.md` — easy-to-break areas

For per-feature implementation details, read `.ai/implementation-notes.md` when touching that feature.

The primary source for requirements is `docs/plan.md` (Japanese).

Build: `dotnet build Trimx.slnx`

Do not add content to this file. Make updates in `.ai/`.
