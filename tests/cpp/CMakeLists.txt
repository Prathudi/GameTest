add_executable(core_tests test_game.cpp)
target_link_libraries(core_tests PRIVATE game_core GTest::gtest_main)

include(GoogleTest)
gtest_discover_tests(core_tests)