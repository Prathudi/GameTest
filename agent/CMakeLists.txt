# The test agent: lets an outside program inspect and control the game.
add_library(agent STATIC src/protocol.cpp)
target_include_directories(agent PUBLIC include)
target_link_libraries(agent
    PUBLIC game_core
    PRIVATE nlohmann_json::nlohmann_json)