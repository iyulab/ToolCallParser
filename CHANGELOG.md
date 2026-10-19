# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

## [0.7.0] - Unreleased

### Added
- **`ToolCallMarkup` reads tool calls an open model wrote as text.** `Read(text)` returns the Hermes / Qwen 2.5
  `<tool_call>{json}</tool_call>` and Qwen3-Coder `<function=Name><parameter=x>…</function>` blocks (wrapper optional) as
  name + arguments, the prose before them, and where a block the text ends inside begins; `Scan(text)` says whether a
  streaming reader should hold output. Deciding which blocks to run stays with the caller.

### Changed
- **Breaking: text that is not JSON has no tool calls.** `Parse`, `HasToolCalls` (factory and every parser) and
  `DetectProvider` answer an empty list / `false` / `Provider.Auto` for a plain-text answer instead of throwing
  `JsonException`. Migration: drop a `catch (JsonException)` around these calls; check `HasToolCalls` instead.
- **Breaking: a call whose arguments are not a JSON object throws `FormatException`** naming the tool, where it used to
  be returned and fail later inside `GetArgument`/`HasArgument`. Migration: catch `FormatException` where a model's
  malformed call should be reported back to it.
- **Breaking: a streaming chunk counts only for calls complete within it.** A `choices[].delta` entry with an empty or
  partial `arguments`, or without a name, is no longer returned as a call (nor reported by `HasToolCalls`). Migration:
  accumulate the stream, then parse the assembled message.
- **`GetArguments<T>()` and `GetArgument<T>()` match property names case-insensitively by default**
  (`ToolCall.DefaultArgumentOptions`); pass options to keep exact matching.

### Fixed
- **Ollama's native response yields its calls.** A `tool_calls` entry without an `id`, or with `arguments` given as an
  object, was dropped (`HasToolCalls` said true, `Parse` returned nothing) or threw; it is now read, with a generated id.
- **The format guide (`docs/tool-call-format-guide.md`) no longer carries a maintenance log** — release history is this file.
- **The package links to the repository as its project site** (`PackageProjectUrl`) — its nuget.org page had no project link.

### Changed
- **The nuget.org «Release Notes» of every package link to the GitHub release of its version**, whose notes are this CHANGELOG's section for that version.

## [0.6.5] - 2026-10-01

### Fixed
- **Every C# example in the README compiles.** The arguments example declared `var args`, which clashes with the
  implicit `args` of a top-level program. The custom-parser skeleton had members with no body; it now throws
  `NotImplementedException` in each, so its member signatures are checked against `IToolCallParser`. A test compiles
  every README block against the current API.

## [0.6.4] - 2026-09-30

### Changed
- **Documentation comments describe behaviour only.** Comments no longer refer to internal tracking or planning records.

## [0.6.3] - 2026-09-28

### Fixed
- **The package carries the README.** Its nuget.org page showed only the one-line description; it now shows the
  README (supported formats, usage, API).

## [0.6.2] - 2026-09-17

This file starts at 0.6.2. Changes in earlier releases were not recorded here; the commit history is
the record for them.
