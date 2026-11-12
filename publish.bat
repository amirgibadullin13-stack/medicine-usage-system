@echo off
rem Builds a single DrugUsageSimple.exe (self-contained: .NET is not required on the target PC).
dotnet publish DrugUsageSimple.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Send the error text above.
  pause
  exit /b 1
)
echo.
echo DONE: publish\DrugUsageSimple.exe
pause
