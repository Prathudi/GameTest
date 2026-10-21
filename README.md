# TestPilot

A C++ in-game test agent and a C# test runner that automate testing of a
sample Snake game, with a Jenkins pipeline that runs the tests on every commit.

> **Status:** work in progress. See the roadmap below.

## Why this project exists

Manual smoke-testing of a game is slow and repetitive. TestPilot explores how
external automated tests can drive a real-time game loop reliably:
frame-synchronized commands, deterministic runs, and CI reporting.

The Snake game is only the **system under test** (original code written for
this project). The framework is the focus.

## Planned architecture

```
 Snake game (C++17)                      C# test runner (.NET 10, NUnit)
 +---------------------------+   TCP    +-----------------------------+
 | game loop <-- Test Agent  | <------> | GameClient + NUnit tests    |
 | (state registry,          |  JSON    | JUnit XML report            |
 |  command queue, step(n))  |          +-----------------------------+
 +---------------------------+                       |
                                          Jenkins: build -> test -> report
```

## Build (Windows, Visual Studio 2022 or later)

```powershell
cmake -S . -B build
cmake --build build --config Release
.\build\game\Release\snake.exe --version
```

## Roadmap

- [x] Phase 0: repo, build skeleton
- [ ] Phase 1: deterministic Snake game core
- [ ] Phase 2: C++ test agent (TCP, state, command queue)
- [ ] Phase 3: C# client library and NUnit tests
- [ ] Phase 4: Jenkins pipeline
- [ ] Phase 5: docs, demo video

## Design decisions

_Filled in as the project grows._

## Bugs I hit

_Filled in as the project grows. Real bugs and how they were fixed._
