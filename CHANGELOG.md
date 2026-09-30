# Changelog

All notable changes to this project are documented in this file. Versions follow
[Semantic Versioning](https://semver.org/); while the major version is 0, a minor release may contain
breaking changes, and each one is marked **Breaking** with a migration note.

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
