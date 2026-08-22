> [!CAUTION]
> `MANUAL VM ONLY — DO NOT EXECUTE BY AGENT`
>
> 真實 Driver installation、device open、IOCTL、memory integration 與 Driver Verifier 結果只能由使用者本人在 Windows 11 VM 填寫。Agent 不得將未執行項目標記為 PASS。

# KernelMemoryLab V1 Verification Report

## 1. Release identity

| Field | Value |
|---|---|
| Product | KernelMemoryLab V1 |
| Driver version | `0.5.0.0` |
| Protocol version | `1.0` |
| Architecture | Windows x64 |
| Capability mask | `0x0000000000000F07` |
| Report date | 2026-08-22 |
| Release state | **RELEASE CANDIDATE — MANUAL VM INTEGRATION PENDING** |

Package completion fields：

```text
Release directory:
ReleaseManifest SHA-256:
Source Phase07 PackageManifest SHA-256:
Source commit:
VM test commit/build:
Tester:
VM completion date:
```

## 2. Final scope assertion

```text
KernelMemoryLab V1 implements kernel-mode access
to an explicitly allowed user-mode laboratory process.

It does NOT implement arbitrary kernel-memory access.
It does NOT implement physical-memory access.
It does NOT implement arbitrary-process access.
It does NOT implement anti-cheat/security bypass.
```

The only allowed successful memory target is `KernelMemoryLab.Target.exe` user virtual memory. The V1 implementation contains no arbitrary kernel VA, physical memory, CR3/page-table, kernel patch, protected-process bypass or third-party security bypass feature.

## 3. Artifact inventory

| Required artifact | Version / location | Static status | Manual VM status |
|---|---|---|---|
| `KernelMemoryLab.Driver.sys` | `Driver/`, `0.5.0.0` | Built; test signature present in Phase 07 package | NOT RUN |
| `KernelMemoryLab.Driver.inf` | `Driver/`, `0.5.0.0` | Inf2Cat signability PASS | NOT RUN |
| `kernelmemorylab.driver.cat` | `Driver/` | Catalog generated; test signature present | NOT RUN |
| `KernelMemoryLab.Target.exe` | `Apps/` | Build PASS; self-memory tests PASS | NOT RUN |
| `KernelMemoryLab.Controller.exe` | `Apps/` | Build PASS; fake-transport tests PASS | NOT RUN |
| `Driver_Install.md` | `Docs/` | Documentation/static review PASS | Actual-flow correction PENDING |
| `API_Usage.md` | `Docs/` | API/source/version cross-check PASS | N/A |
| `VM_Test_Checklist.md` | `Docs/` | T01–T10 checklist present | T01–T10 NOT RUN |
| `Verification_Report.md` | `Docs/` | Present | User result completion PENDING |

Release manifest hashes are authoritative for the packaged files; do not mix artifacts from different packages.

## 4. Agent-permitted verification

The following results do not load, install, start, stop, call or verify a running Driver.

| Check | Result | Evidence / observation |
|---|---|---|
| Release x64 restore/build | PASS | MSBuild exit code 0 |
| Driver INF signability / catalog generation | PASS | Inf2Cat: 0 errors, 0 warnings |
| Driver C/C++ static analysis | PASS | 45 functions analyzed; 0 reported errors/warnings |
| Protocol unit tests | PASS | 16 passed, 0 failed |
| Target user-mode self-memory tests | PASS | 6 passed, 0 failed |
| Controller fake-transport tests | PASS | 7 passed, 0 failed |
| Source Phase 07 manifest hashes | PASS | 21 files; 0 mismatches |
| Source Phase 07 private-key scan | PASS | 0 private-key files |
| Real Driver execution by Agent | NOT PERFORMED | Mandatory safety boundary |

Notes：本機 PowerShell execution policy 阻止直接啟動 `scripts/build.ps1`；Agent 未修改或繞過 policy，而是使用該腳本內等效的 MSBuild restore/build arguments。這不影響 build output，但應保留於稽核紀錄。

## 5. Manual Windows 11 VM integration

Source of truth：`docs/VM_Test_Checklist.md`。使用者完成每項後，將以下 `NOT RUN` 改為實際結果並附 evidence filename。

| Test | Required result | Actual result | Evidence | Notes |
|---|---|---|---|---|
| T01 Driver package/service/device | PASS | NOT RUN | | |
| T02 Protocol/version/capabilities | PASS | NOT RUN | | |
| T03 Target PID/addresses/refresh | PASS | NOT RUN | | |
| T04 Single read | PASS | NOT RUN | | |
| T05 Single write/UI refresh | PASS | NOT RUN | | |
| T06 Batch read | PASS | NOT RUN | | |
| T07 Batch write/read-back | PASS | NOT RUN | | |
| T08 Bounded negative validation | PASS | NOT RUN | | |
| T09 Repeated normal operations | PASS | NOT RUN | | |
| T10 Driver Verifier | Optional | NOT RUN / SKIPPED | | |
| Driver removal / VM restoration | PASS | NOT RUN | | |

```text
Overall Phase 08 Result: PENDING MANUAL VM VERIFICATION
VM OS/build:
Snapshot:
PackageManifest SHA-256:
Tester:
Completion time:
Open failures:
```

## 6. Driver install-guide reconciliation

After T01 and cleanup, the user must compare the actual Windows 11 VM workflow with `docs/Driver_Install.md`：

- [ ] Package inspection commands matched actual files.
- [ ] Certificate trust steps matched actual VM behavior.
- [ ] Primitive-driver install command succeeded as documented.
- [ ] Published `oem#.inf` discovery was accurate.
- [ ] Service/device query and Controller open behavior matched.
- [ ] Stop, package removal, certificate cleanup and snapshot restore matched.
- [ ] Actual error codes or missing prerequisites were added to troubleshooting.

Current status：**PENDING ACTUAL VM FLOW**。

## 7. API and protocol reconciliation

- [x] `KernelMemoryApi` public signatures checked against `docs/API_Usage.md`.
- [x] Protocol version checked: `1.0`.
- [x] Driver version checked: `0.5.0.0`.
- [x] Capability mask checked: `0x0000000000000F07`.
- [x] Limits checked: single `4096`, batch items `128`, aggregate `524288`.
- [x] API documentation covers Open, Close, protocol/capabilities/Ping, raw single/batch and six typed helpers.
- [x] Each required API documents purpose, parameters, return type, validation, errors, minimal example and thread-safety/lifetime.

## 8. Final release gate

All rows must be PASS before changing release state from release candidate to final.

| Gate | Status | Blocking reason / evidence |
|---|---|---|
| Agent Build | PASS | Release x64 build succeeded |
| Agent Static/Unit Tests | PASS | Static analysis and 29 user-mode tests passed |
| User VM Integration | **PENDING** | Phase 08 results not supplied |
| Install Guide corrected from actual VM flow | **PENDING** | Requires T01/install/remove observations |
| API Guide matches binary/protocol | PASS | Source/constants cross-check completed |

```text
Final Release Gate: PENDING
Blocking items:
- User VM Integration PASS evidence is required.
- Driver_Install.md must be reconciled with the actual VM flow.
```

The current artifact is a **release candidate**, not a verified final release.

## 9. User sign-off

Only complete this section after T01–T09 and cleanup are PASS：

```text
I manually executed the Phase 08 checklist in a disposable Windows 11 x64 VM.
I verified that the package and build identifiers match this report.
I attached evidence for T01–T09 and cleanup.
I confirmed that Driver_Install.md matches the actual install/remove workflow.

Tester name:
Date/time:
Overall Phase 08 Result: PASS / FAIL
Final Release Gate: PASS / FAIL
Signature/approval reference:
```
