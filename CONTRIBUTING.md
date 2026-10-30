# Contributing

Report bugs and suggest improvements in GitHub Issues. Include the Windows version,
display resolution/scaling, reproduction steps and expected versus actual behavior.
Never attach private desktop captures, local configuration, credentials or unsanitized logs.
Follow [SECURITY.md](SECURITY.md) for vulnerabilities.

Fork the repository, create a focused feature branch, build on Windows with .NET SDK 8,
and submit a pull request describing the change and validation. Keep dependencies minimal.

```powershell
dotnet build src/EyeCarer/EyeCarer.csproj -c Release
dotnet run --project tests/EyeCarer.Checks -c Release
dotnet publish src/EyeCarer/EyeCarer.csproj -c Release -r win-x64 --self-contained true -o publish
```

For desktop changes, manually check Settings, Save/Cancel, Preview, Esc, tray Exit,
and startup behavior on Windows. Test multiple displays and scaling when relevant.
`scripts/Validate.ps1` is an optional interactive smoke/stress test: it temporarily
changes the published configuration and restarts EyeCarer. Read it before running;
its finally block restores 900/15 seconds and starts the app.

By contributing, you agree to license your contribution under the project's MIT license.
