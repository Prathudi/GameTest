# GameTest

An automated testing framework for a small 2D game: a C++ in-game test agent and a
C# test runner that executes JSON-defined tests, validates game behavior, and saves
screenshots and logs when a test fails.

> **Status:** work in progress.

## Build (Windows, Developer PowerShell for Visual Studio)

```powershell
cmake -S . -B build
cmake --build build --config Release
.\build\game\Release\game.exe
```
## Credits

Font: *Press Start 2P* by The Press Start 2P Project Authors, used under the
SIL Open Font License 1.1 (see `assets/fonts/OFL.txt`).