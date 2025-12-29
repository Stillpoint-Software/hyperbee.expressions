# .NET 8.0 and .NET 10.0 Multi-Targeting Upgrade Plan

## Table of Contents
- [1. Executive Summary](#1-executive-summary)
- [2. Migration Strategy](#2-migration-strategy)
- [3. Detailed Dependency Analysis](#3-detailed-dependency-analysis)
- [4. Project-by-Project Plans](#4-project-by-project-plans)
- [5. Risk Management](#5-risk-management)
- [6. Testing & Validation Strategy](#6-testing--validation-strategy)
- [7. Complexity & Effort Assessment](#7-complexity--effort-assessment)
- [8. Source Control Strategy](#8-source-control-strategy)
- [9. Success Criteria](#9-success-criteria)

---

## 1. Executive Summary

### Scenario Description
Upgrade all projects in the solution to multi-target .NET 8.0 (LTS) and .NET 10.0 (LTS).

### Scope
- 5 SDK-style projects
- All projects currently target net8.0/net9.0 or net8.0/net10.0
- No package or API compatibility issues
- No security vulnerabilities
- Simple dependency graph, no cycles
- Total LOC: ~13,000

### Selected Strategy
**All-At-Once Strategy** - All projects upgraded simultaneously in a single operation.

**Rationale:**
- Small solution (5 projects)
- All projects already multi-targeted
- No blocking issues or vulnerabilities
- All packages compatible with target frameworks

### Complexity Assessment
- Solution classified as Simple
- No high-risk or complex migration steps expected

### Critical Issues
- None identified

### Iteration Strategy
- Single batch, all projects upgraded together
- Plan generated in 3 main phases: skeleton, detail, validation

## 2. Migration Strategy

### Approach Selection
**All-At-Once Strategy**
- All projects will be updated to multi-target .NET 8.0 and .NET 10.0 in a single, atomic operation.

### Justification
- Solution is small (5 projects), all SDK-style, and already multi-targeted.
- No incompatible packages or APIs.
- No security vulnerabilities or high-risk dependencies.
- Dependency graph is simple and acyclic.

### Execution Order
- All project files and shared MSBuild imports will be updated simultaneously.
- No need for phased or tiered migration.
- After upgrade, all projects will be built and tested together.

### Phase Definitions
- **Phase 0:** Preparation (SDK validation, global.json update if needed)
- **Phase 1:** Atomic Upgrade (update all project files, multi-target net8.0/net10.0)
- **Phase 2:** Test Validation (run all tests, verify build and runtime compatibility)

## 3. Detailed Dependency Analysis

### Dependency Graph Summary
- All projects are SDK-style and use explicit multi-targeting.
- No circular dependencies detected.
- Dependency relationships:
  - `Hyperbee.Expressions.csproj` is the core library, depended on by Lab, Tests, and Benchmark projects.
  - `Hyperbee.Expressions.Lab.csproj` depends on the core library and is tested by Tests.
  - `Hyperbee.Expressions.Tests.csproj` depends on both Lab and core library.
  - `Hyperbee.Expressions.Benchmark.csproj` depends on the core library.
  - `docs.shproj` is standalone.

### Project Groupings for Migration
- All projects will be upgraded in a single atomic operation (All-at-Once Strategy).
- No intermediate states or phased migration required.

### Critical Path Identification
- The core library (`Hyperbee.Expressions.csproj`) is the main dependency for all other projects.
- All projects can be upgraded together due to compatible dependencies and multi-targeting support.

### Circular Dependency Details
- None present.

## 4. Project-by-Project Plans

### docs\docs.shproj
**Current State:** net8.0;net10.0, SDK-style, 0 files, 0 LOC, no dependencies
**Target State:** net8.0;net10.0
**Migration Steps:**
1. Ensure multi-targeting for net8.0 and net10.0
2. Validate build

### src\Hyperbee.Expressions.Lab\Hyperbee.Expressions.Lab.csproj
**Current State:** net8.0;net9.0, SDK-style, 6 files, 748 LOC, depends on core
**Target State:** net8.0;net10.0
**Migration Steps:**
1. Update TargetFrameworks to net8.0;net10.0
2. Validate build and tests

### src\Hyperbee.Expressions\Hyperbee.Expressions.csproj
**Current State:** net8.0;net9.0, SDK-style, 43 files, 5390 LOC, core library
**Target State:** net8.0;net10.0
**Migration Steps:**
1. Update TargetFrameworks to net8.0;net10.0
2. Validate build and tests

### test\Hyperbee.Expressions.Benchmark\Hyperbee.Expressions.Benchmark.csproj
**Current State:** net9.0, SDK-style, 3 files, 259 LOC, depends on core
**Target State:** net8.0;net10.0
**Migration Steps:**
1. Update TargetFrameworks to net8.0;net10.0
2. Validate build

### test\Hyperbee.Expressions.Tests\Hyperbee.Expressions.Tests.csproj
**Current State:** net8.0;net9.0, SDK-style, 32 files, 6508 LOC, depends on core and lab
**Target State:** net8.0;net10.0
**Migration Steps:**
1. Update TargetFrameworks to net8.0;net10.0
2. Validate build and tests

## 5. Risk Management

### High-Risk Changes Table
| Project | Risk Level | Description | Mitigation |
|---------|------------|-------------|------------|
| All     | Low        | No breaking changes, all packages compatible | Full build and test validation after upgrade |

### Security Vulnerabilities
- None identified in assessment

### Contingency Plans
- If any build or test failures occur, revert to previous branch and investigate compatibility issues.
- If package incompatibility is discovered, isolate and address per project.

## 6. Testing & Validation Strategy

### Phase-by-Phase Testing Requirements
- After atomic upgrade, build the entire solution.
- Run all test projects:
  - test/Hyperbee.Expressions.Tests/Hyperbee.Expressions.Tests.csproj
  - test/Hyperbee.Expressions.Benchmark/Hyperbee.Expressions.Benchmark.csproj
- Validate:
  - All projects build with 0 errors and 0 warnings
  - All tests pass
  - No runtime issues on both net8.0 and net10.0

### Validation Checklist
- [ ] Solution builds successfully for both target frameworks
- [ ] All tests pass for both target frameworks
- [ ] No package or API compatibility issues
- [ ] No new warnings or vulnerabilities

## 7. Complexity & Effort Assessment

| Project | Complexity | Dependencies | Risk |
|---------|------------|--------------|------|
| docs.shproj | Low | None | Low |
| Hyperbee.Expressions.Lab | Low | Core | Low |
| Hyperbee.Expressions | Low | None | Low |
| Hyperbee.Expressions.Benchmark | Low | Core | Low |
| Hyperbee.Expressions.Tests | Low | Core, Lab | Low |

- All projects are low complexity due to SDK-style, compatible packages, and no breaking changes.
- No additional resources or parallelization required.

## 8. Source Control Strategy

- Use current working branch for upgrade (as requested)
- All changes for multi-targeting net8.0 and net10.0 will be committed in a single atomic commit
- PR should include:
  - All project file updates
  - Build and test validation results
- Reviewers should verify:
  - All projects build for both frameworks
  - All tests pass
  - No regressions or new warnings

## 9. Success Criteria

### Technical Criteria
- All projects multi-target net8.0 and net10.0
- All package references remain compatible
- Solution builds with 0 errors and 0 warnings for both frameworks
- All tests pass for both frameworks
- No security vulnerabilities or regressions

### Quality Criteria
- Code quality and test coverage maintained
- Documentation updated if needed

### Process Criteria
- All-at-Once strategy followed
- Single atomic commit for all changes
- Source control and review process completed
