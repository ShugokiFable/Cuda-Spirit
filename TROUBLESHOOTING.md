# Troubleshooting

## First-run build fails

- Keep the full extracted folder together.
- Confirm Windows can reach `dot.net`, NuGet, and Microsoft download services.
- Temporarily allow PowerShell and `dotnet.exe` through security software.
- Read `logs\install.log`.
- Run `REBUILD_CUDA_SPIRIT.bat` after correcting the error.

## The app opens but rendered official pages fail

Install or repair the Microsoft Edge WebView2 Runtime. Basic cached guides, deterministic safety engines, market APIs, and local data can still work when rendered fallback is unavailable.

## A source is degraded

Open **Live Data Center**. Cuda Spirit keeps cached records and retries degraded sources after a controlled interval. Maintenance, regional website changes, API limits, or bot protection can temporarily affect one feed without disabling the others.

## Profile sync fails

- Confirm the official adventurer profile is public.
- Paste the complete profile link.
- Confirm the app region matches the profile region.
- Retry after website maintenance.

## Reset local app data

Close Cuda Spirit and rename or delete:

```text
%APPDATA%\CudaSpirit
```

Back it up first to preserve tasks, settings, decisions, and history.
