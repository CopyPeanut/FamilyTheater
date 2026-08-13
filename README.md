# FamilyTheater

Windows desktop media manager built with WPF and .NET 8.

## Internal exe installer

This repository uses Inno Setup to build a single `.exe` installer for internal use.

Build the installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Installer output:

```text
artifacts/installer
```

The `artifacts` directory is ignored by Git and can be regenerated at any time.
