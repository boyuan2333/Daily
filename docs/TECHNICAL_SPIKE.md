# Technical Spike: Windows Desktop Route Selection

## 1. Actual Environment Evidence

All values below are direct command results collected on the target machine on 2026-07-25.

| Area | Command | Actual result |
| --- | --- | --- |
| Windows system information | `Get-ComputerInfo | Select-Object WindowsProductName, WindowsDisplayVersion, WindowsVersion, OsBuildNumber` | `WindowsProductName=Windows 10 Pro`; `WindowsDisplayVersion=` (empty); `WindowsVersion=2009`; `OsBuildNumber=26200` |
| Windows registry identity | `Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' | Select-Object ProductName, DisplayVersion, CurrentBuild, CurrentBuildNumber, UBR` | `ProductName=Windows 10 Pro`; `DisplayVersion=25H2`; `CurrentBuild=26200`; `CurrentBuildNumber=26200`; `UBR=8655` |
| .NET SDK | `dotnet --list-sdks` | No output; no .NET SDK is currently installed |
| .NET runtimes | `dotnet --list-runtimes` | `Microsoft.NETCore.App` 6.0.9/7.0.9 and `Microsoft.WindowsDesktop.App` 6.0.9/7.0.9 |
| Node.js and npm | `node --version`; `npm --version` | Node `v24.11.1`; npm `11.13.0` |
| Git | `git --version` | `2.52.0.windows.1` |
| Visual Studio Build Tools | `vswhere` | Visual Studio 2022 Build Tools `17.14.36717.8` is installed |
| Managed Desktop workload fact | `vswhere -requires Microsoft.VisualStudio.Workload.ManagedDesktop` | No matching installation returned |
| MSBuild PATH fact | `where msbuild` | Not found on PATH |
| Windows SDK tools | Windows Kits file enumeration | `makeappx.exe` and `signtool.exe` are present under Windows SDK `10.0.26100.0` directories but are not on PATH |
| Package manager | `winget.exe`; Desktop App Installer query | `winget.exe` is available; Desktop App Installer `1.29.280.0` is installed |

## 2. Windows Identity Discrepancy

The two required sources agree on `Windows 10 Pro` and build `26200`. They differ in release-label fields: `Get-ComputerInfo` reports an empty `WindowsDisplayVersion` and `WindowsVersion=2009`, while the registry reports `DisplayVersion=25H2`.

This document records the discrepancy only. It does not infer or assign a definitive Windows release/version from those fields.

## 3. Confirmed Technical Decisions

| Decision | Confirmed direction |
| --- | --- |
| Desktop framework | WinUI 3 + .NET 10 LTS |
| Local persistence | SQLite |
| Development distribution | unpackaged local debug first |
| MSIX | deferred to MVP-2 |

Tray implementation, global-shortcut wrapper, startup registration mechanism, UI automation framework, and final packaging flow are not selected by this record.

## 4. Required and Conditional Prerequisites

### Required Now

| Component | Why | Minimum action |
| --- | --- | --- |
| .NET 10 SDK | Provides the `dotnet` CLI for the disposable WinUI Spike | Install the .NET 10 SDK only |

### Conditional

| Component | When it is needed | Status |
| --- | --- | --- |
| Windows App SDK template / CLI package support | Only if the .NET 10 CLI cannot create, restore, build, and run the minimal WinUI 3 Spike | Evaluate after CLI preflight |
| WinUI / Managed Desktop workload | Only if the CLI route cannot complete the Spike or an IDE workflow is explicitly selected | Conditional; not an MVP-0 absolute blocker |
| Visual Studio IDE tooling | Only for an explicitly selected IDE authoring or debugging workflow | Conditional; not required for CLI-based MVP-0 |

The missing workload and absent `msbuild` PATH entry are environment facts, not current MVP-0 absolute blockers. First verify whether the .NET CLI can create, compile, and run the minimal WinUI Spike.

## 5. Spike Status and Exact Success Conditions

> Waiting for authorization to install the .NET 10 SDK.

After authorization and installation, the disposable `spikes/winui3-preflight` Spike succeeds only when all of the following are observed:

1. `dotnet --version` displays an installed .NET 10 SDK.
2. The CLI creates or restores a minimal WinUI 3 Spike without requiring Visual Studio IDE tooling.
3. `dotnet build` succeeds without errors.
4. The local Spike starts one empty Windows window and closes cleanly.
5. At least one of a tray entry or global shortcut is verified and released cleanly on shutdown.
6. A SQLite sentinel write is acknowledged after local durable storage reports success, and an independent subsequent run reads the same sentinel value.
7. The Spike contains no formal product business logic: no routes, steps, captures, pauses, snapshots, or Guide mode behavior.

## 6. Unresolved Risks

- The selected WinUI tray approach remains unverified.
- Global shortcut registration conflicts, application lifetime, and shutdown release remain unverified.
- SQLite package restore and Windows deployment behavior remain unverified until the Spike runs.
- The deferred MSIX decision still affects startup registration, signing, updates, and local-data locations.
- Desktop UI automation tooling and the CI environment remain unselected.
- The Windows identity field discrepancy must be rechecked when selecting a supported Windows App SDK release, without treating the current fields as a definitive release label.

## 7. Proposed MVP-0 File Structure

This is a proposed future layout only. It is not created by this Spike.

```text
src/
  ExecutionContinuity.Domain/
    Routes/
    Steps/
    Snapshots/
    Inbox/
    ExecutionState/
  ExecutionContinuity.Persistence/
    Storage/
    Migrations/
    Repositories/
  ExecutionContinuity.App/
    App/
    Windows/
    Composition/
tests/
  ExecutionContinuity.Domain.Tests/
  ExecutionContinuity.Persistence.Tests/
spikes/
  winui3-preflight/   # disposable; not a product project
```

MVP-0 begins with Domain and Persistence. The future App project is a composition boundary and must not contain MVP-1 planning or Guide mode behavior during MVP-0.

## 8. Authorization Needed

1. Approve installation of the .NET 10 SDK only.
2. After CLI preflight, approve conditional Windows App SDK template/package support only if the CLI route cannot create, build, and run the Spike.
