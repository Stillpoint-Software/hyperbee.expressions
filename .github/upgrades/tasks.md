# hyperbee.expressions .NET Multi-Targeting Upgrade Tasks

## Overview

This document tracks the upgrade of all projects in the solution to multi-target .NET 8.0 and .NET 10.0. All project files will be updated in a single atomic operation, followed by full build and test validation, and a single commit at the end.

**Progress**: 0/3 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Verify prerequisites
**References**: Plan §2 Migration Strategy, Plan §Phase 0

- [✓] (1) Verify required .NET 8.0 and .NET 10.0 SDKs are installed per Plan §Phase 0
- [▶] (2) SDK versions meet minimum requirements (**Verify**)
- [ ] (3) If present, update `global.json` to include net8.0 and net10.0 SDKs per Plan §Phase 0
- [ ] (4) `global.json` (if present) is compatible with target SDKs (**Verify**)

---

### [ ] TASK-002: Atomic multi-targeting upgrade for all projects
**References**: Plan §Phase 1, Plan §4 Project-by-Project Plans

- [ ] (1) Update `TargetFrameworks` in all project files to `net8.0;net10.0` per Plan §4
- [ ] (2) All project files multi-target net8.0 and net10.0 (**Verify**)
- [ ] (3) Restore all dependencies for the solution
- [ ] (4) All dependencies restored successfully (**Verify**)
- [ ] (5) Build the entire solution for both target frameworks
- [ ] (6) Solution builds with 0 errors and 0 warnings for both frameworks (**Verify**)

---

### [ ] TASK-003: Run and validate all tests, then commit changes
**References**: Plan §Phase 2, Plan §6 Testing & Validation Strategy, Plan §8 Source Control Strategy

- [ ] (1) Run all tests in `test/Hyperbee.Expressions.Tests/Hyperbee.Expressions.Tests.csproj` and `test/Hyperbee.Expressions.Benchmark/Hyperbee.Expressions.Benchmark.csproj` for both net8.0 and net10.0
- [ ] (2) Fix any test failures (reference Plan §4 and §6 for details)
- [ ] (3) Re-run tests after fixes
- [ ] (4) All tests pass with 0 failures for both frameworks (**Verify**)
- [ ] (5) Commit all changes with message: "TASK-003: Complete multi-targeting upgrade to .NET 8.0 and .NET 10.0"

---

