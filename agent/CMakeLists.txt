# The test agent: lets an outside program inspect and control the game.
find_package(Threads REQUIRED)

add_library(agent STATIC src/protocol.cpp src/agent_server.cpp)
target_include_directories(agent PUBLIC include)
target_link_libraries(agent
    PUBLIC game_core SFML::Network Threads::Threads
    PRIVATE nlohmann_json::nlohmann_json)