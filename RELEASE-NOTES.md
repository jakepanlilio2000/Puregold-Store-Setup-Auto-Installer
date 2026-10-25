# Store Setup 4.1.0

- Read-only readiness report for elevation, Windows compatibility, MSI activity, conflicting selections, package availability, and estimated disk space.
- Explicit, opt-in Windows settings. Firewall changes are disabled by default.
- Release catalog containing package versions, file sizes, and SHA-256 hashes. Invalid cached files are restored selectively and checked before execution.
- Persistent package results and attempt counts, individual/all-failed retries, and manual resume after restarting Windows or reopening the app.
- Durable checkpoint writes with recovery from the previous valid checkpoint.
- Release output moved to publish/ with the executable, 7-Zip files, bookmark script, encrypted archive, catalog, and documentation.

The current source archive contains 68 files. Oracle Java Runtime is still absent from the supplied assets. Add its authorized installer, rebuild the release, and reopen the application to retry it.

Requires the .NET 9 Desktop Runtime (x64). The automated suite does not run third-party installations or change workstation settings; full deployment and Wamp service testing still require a disposable workstation or VM.
