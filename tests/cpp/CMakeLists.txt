include(GoogleTest)
find_package(Threads REQUIRED)

# Tests for the game rules.
add_executable(core_tests test_game.cpp)
target_link_libraries(core_tests PRIVATE game_core GTest::gtest_main)
gtest_discover_tests(core_tests)

# Tests for the agent (requests, the command queue, line splitting, the TCP server).
add_executable(agent_tests test_protocol.cpp test_command_queue.cpp
                           test_line_buffer.cpp test_agent_server.cpp)
target_link_libraries(agent_tests PRIVATE agent nlohmann_json::nlohmann_json
                                          GTest::gtest_main Threads::Threads)
gtest_discover_tests(agent_tests)