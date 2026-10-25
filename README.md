# Puregold Store Setup 4.1

Windows WPF installer for Zone 11 workstations. Requires Windows administrator access and the .NET 9 Desktop Runtime (x64). Build with the .NET 10 SDK; the application continues to target .NET 9.

## Release files

Keep `PGInstaller.exe`, `7z.exe`, `7z.dll`, `cbm.ps1`, `assets.manifest.json`, and `assets.zip` together. Run `PGInstaller.exe` and enter the existing access key. Select a department, review the application checklist, and start installation. Installed applications can be selected with **Reinstall**. Progress and results remain visible at the bottom; **Activity** contains the log.

`assets.zip` uses ZIP AES-256 encryption with password `pw@1234`. Both flat archives and archives containing an `assets/` folder are supported. The installer checks existing files before extracting only the requested package into `C:\Assets\Packages`. Missing files are checked again in `assets.zip`; unrelated packages stay compressed. Temporary package contents use a private session directory and are cleaned up on exit.

FSDM installs the OpenOffice MSI and its CAB from `FSDM.zip` silently before deploying FSDevMan. HRD defaults to `wamp2.exe` at `C:\wamp`, then replaces that directory with the `wamp/` content from `wampHR.zip`. The original directory is retained at `C:\wamp.PGBackup-*`. Running services belonging to that Wamp installation are stopped before replacement and restarted afterward. A failed activation attempts to roll back the content; a retained backup path is logged if rollback cannot finish.

## Readiness and Windows settings

**Check readiness** performs a read-only check of administrator access, 64-bit Windows compatibility, active MSI transactions, conflicting version selections, required assets, and estimated disk space. The same checks run automatically when starting or resuming a deployment. Selected packages are extracted and verified before computer rename, domain join, restore-point creation, or application installation. Missing packages are shown in **Readiness**; available packages can continue after review.

Wallpaper, power settings, taskbar replacement, appearance optimizations, and firewall disabling are separate, unchecked options under **Optional Windows settings**. Firewall disabling requires explicitly selecting its checkbox. These settings run after the package queue finishes. Restore-point creation and domain joining retain their own controls.

`assets.manifest.json` records each asset's version, byte length, and SHA-256 digest. The installer rejects stale or corrupt cache files, restores the required files from the archive, and verifies their hashes before use. The catalog must match the application's release version. Keep it with the executable and archive.

## Retry and resume

**Results** shows each package's status, attempt count, and failure details. Use a row's **Retry** button for one failed or unavailable package, or **Retry failed** for all failures. Successful packages remain completed. If assets were added or changed, rebuild the catalog/archive and reopen Store Setup before retrying.

The queue and selected Windows options are saved at `%ProgramData%\PGInstaller\install-state.json` before and after each package, with a backup of the previous valid checkpoint. If an installer requests a restart, the queue pauses at a package boundary. Restart Windows, reopen the installer, enter the access key, and choose **Resume**. An interrupted in-progress package is retried; completed packages are preserved. Resume requires the same computer, app version, and asset catalog. No automatic startup entry is installed.

## Build and package

```powershell
.\scripts\Build-Release.ps1
```

The default asset source is `F:\IT Files\PG Installer (Zone 11)\assets`. The script publishes the application and creates a fresh, encrypted `publish\assets.zip`, verifies it with 7-Zip, then replaces the previous output archive. Source files are preserved. `-AssetsPath` and `-OutputPath` override the locations; `-AssetsOnly` compresses without publishing.

```powershell
dotnet build PGInstaller.slnx
dotnet run --project tests/PGInstaller.Checks
dotnet run --project tests/PGInstaller.Checks -- --audit
```

The checks cover selective extraction and SHA-256 recovery, readiness blockers, journal persistence and recovery, failed-package retries, reboot resume, HRD content replacement and rollback, process exit codes, and rendered WPF bindings. UI previews are saved under `artifacts\previews`. `--audit` checks the packaged archive against the package manifest and performs a real selective extraction against the current release catalog.

If NuGet is unavailable and the local cache contains the required dependencies and .NET 9.0.20 packs, use `-p:PGOfflineBuild=true -p:RestoreSources="$env:USERPROFILE\.nuget\packages" -p:NuGetAudit=false` with the .NET commands, or `-Offline` with the release script. This fallback only affects builds explicitly opting into it.

## Supplied asset gap

The supplied folder does not contain `jre.exe` or `java.exe` for **Oracle Java Runtime**. Add the authorized Java installer to the source folder and rebuild the archive, or deselect Java. Missing packages are listed before installation; continuing skips those packages explicitly.

## Validation limits

The release has been compiled, its UI rendered, and its archive tested. Third-party installers, domain changes, network changes, and Wamp service replacement still require deployment testing on a disposable workstation or VM. Windows setting changes are opt-in. Disk space is a conservative estimate, not an exact requirement for every third-party installer.

OpenOffice's MSI silent-install options follow the [Apache OpenOffice administration guide](https://wiki.services.openoffice.org/wiki/Documentation/Administration_Guide/Windows). Windows Installer success and restart codes follow [Microsoft's installer error-code documentation](https://learn.microsoft.com/en-us/windows/win32/msi/error-codes).
