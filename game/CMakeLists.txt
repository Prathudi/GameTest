add_subdirectory(core)

add_executable(game app/main.cpp app/renderer.cpp)
target_link_libraries(game PRIVATE game_core SFML::Graphics)

# Where the game finds its assets (fonts). Baked in at build time.
target_compile_definitions(game PRIVATE GAMETEST_ASSET_DIR="${PROJECT_SOURCE_DIR}/assets")