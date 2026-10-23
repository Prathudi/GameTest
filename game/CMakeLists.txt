add_subdirectory(core)

add_executable(game app/main.cpp)
target_link_libraries(game PRIVATE SFML::Graphics)