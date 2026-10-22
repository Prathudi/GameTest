cmake_minimum_required(VERSION 3.28)
project(GameTest VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# ---- Dependencies (downloaded and built by CMake) ----
# (none yet)

# ---- Strict warnings for OUR code only (set after the dependencies on purpose) ----
if(MSVC)
  add_compile_options(/W4 /permissive-)
else()
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

# ---- Our code ----
add_subdirectory(game)