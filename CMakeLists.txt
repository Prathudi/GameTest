cmake_minimum_required(VERSION 3.28)
project(GameTest VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ---- Dependencies (downloaded and built by CMake) ----
include(FetchContent)

# Build libraries as static (no extra DLLs to copy) and skip SFML audio (unused).
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(SFML_BUILD_AUDIO OFF CACHE BOOL "" FORCE)

FetchContent_Declare(SFML
    GIT_REPOSITORY https://github.com/SFML/SFML.git
    GIT_TAG 3.0.2
    GIT_SHALLOW ON
    EXCLUDE_FROM_ALL
    SYSTEM)

# GoogleTest: the C++ unit-test framework.
set(gtest_force_shared_crt ON CACHE BOOL "" FORCE)  # avoid MSVC runtime mismatch
set(INSTALL_GTEST OFF CACHE BOOL "" FORCE)
FetchContent_Declare(googletest
    URL https://github.com/google/googletest/archive/refs/tags/v1.15.2.zip
    DOWNLOAD_EXTRACT_TIMESTAMP TRUE)

FetchContent_MakeAvailable(SFML googletest)

# ---- Strict warnings for OUR code only (set after the dependencies on purpose) ----
if(MSVC)
  add_compile_options(/W4 /permissive-)
else()
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# ---- Our code ----
enable_testing()
add_subdirectory(game)
add_subdirectory(tests/cpp)