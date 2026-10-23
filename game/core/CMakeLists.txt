# The game rules: no graphics, no keyboard hardware, no network.
add_library(game_core STATIC src/game.cpp)
target_include_directories(game_core PUBLIC include)