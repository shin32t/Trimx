# AI context

Shared context for AI coding tools. It is kept separate from the human-facing documentation (`README.md`, `README.ja.md`, `docs/`).

## Contents

| File | Contents |
|---|---|
| [project.md](project.md) | Overview, status, tech stack, build, release, license |
| [architecture.md](architecture.md) | Layering and design rules (commands, ownership, rendering, file I/O) |
| [conventions.md](conventions.md) | Coding conventions and working rules |
| [pitfalls.md](pitfalls.md) | Easy-to-break areas and notes on verification |
| [implementation-notes.md](implementation-notes.md) | Per-feature implementation notes (behavior, class names, rationale). Read only when touching that feature |

The primary source for requirements is `docs/plan.md` (written in Japanese and maintained by humans).

## Entry points per tool

Each tool reads a fixed location, so the files below only point to `.ai/`. **Add content to `.ai/` only.**

| Tool | Entry point |
|---|---|
| Codex / other tools that support AGENTS.md | `AGENTS.md` |
| Gemini CLI | `GEMINI.md` (imports `.ai/` with `@`) |
| GitHub Copilot | `.github/copilot-instructions.md` |
| Cursor | `.cursor/rules/trimx.mdc` |
| Claude Code | `CLAUDE.md` (imports `.ai/` with `@`) |

`implementation-notes.md` is large, so no entry point imports it by default. Tools are told to read it when needed.

## Update rules

- When you implement or change a feature, update the relevant part of `implementation-notes.md`.
- When a design rule or convention changes, update `.ai/`.
- When the specification changes, update `docs/plan.md` as well.
- Do not duplicate content in the entry-point files (it causes drift).
