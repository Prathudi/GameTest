cmake_minimum_required(VERSION 3.20)
project(TestPilot VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Strict warnings: treat the compiler as a free code reviewer.
if(MSVC)
  add_compile_options(/W4 /permissive-)
else()
  add_compile_options(-Wall -Wextra -Wpedantic)
endif()

add_subdirectory(agent)   # C++ test agent (static library)
add_subdirectory(game)    # Sample Snake game = the system under test
