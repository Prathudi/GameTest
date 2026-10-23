#include <gtest/gtest.h>

#include <nlohmann/json.hpp>
#include <string>

#include "gametest/agent/protocol.h"
#include "gametest/game.h"

using nlohmann::json;
using gametest::Game;
using gametest::Scene;
using gametest::agent::handleRequest;
using gametest::agent::Reply;

namespace {

// Sends one request and parses the reply text as JSON.
json call(Game& game, const std::string& request) {
    return json::parse(handleRequest(request, game).json);
}

Game newGame() { return Game(Game::defaultLevel()); }  // player starts at (1,1)

}  // namespace

TEST(Protocol, PingAnswersPong) {
    Game g = newGame();
    const json r = call(g, R"({"cmd":"ping"})");
    EXPECT_TRUE(r["ok"].get<bool>());
    EXPECT_EQ(r["reply"].get<std::string>(), "pong");
}

TEST(Protocol, UnknownCommandIsAnError) {
    Game g = newGame();
    const json r = call(g, R"({"cmd":"fly"})");
    EXPECT_FALSE(r["ok"].get<bool>());
    EXPECT_NE(r["error"].get<std::string>().find("unknown command"), std::string::npos);
}

TEST(Protocol, InvalidJsonIsAnError) {
    Game g = newGame();
    const json r = call(g, "this is not json");
    EXPECT_FALSE(r["ok"].get<bool>());
    EXPECT_NE(r["error"].get<std::string>().find("invalid json"), std::string::npos);
}

TEST(Protocol, RequestWithoutACommandIsAnError) {
    Game g = newGame();
    EXPECT_FALSE(call(g, R"({"foo":1})")["ok"].get<bool>());
    EXPECT_FALSE(call(g, R"([1,2,3])")["ok"].get<bool>());
    EXPECT_FALSE(call(g, R"({"cmd":5})")["ok"].get<bool>());
}

TEST(Protocol, KeyEnterStartsTheGame) {
    Game g = newGame();
    ASSERT_EQ(g.scene(), Scene::MainMenu);
    const json r = call(g, R"({"cmd":"key","key":"Enter"})");
    EXPECT_TRUE(r["ok"].get<bool>());
    EXPECT_EQ(g.scene(), Scene::Playing);
}

TEST(Protocol, KeyMovesThePlayer) {
    Game g = newGame();
    call(g, R"({"cmd":"key","key":"Enter"})");
    call(g, R"({"cmd":"key","key":"Right"})");
    const json s = call(g, R"({"cmd":"get_state"})")["state"];
    EXPECT_EQ(s["player"]["x"].get<int>(), 2);
    EXPECT_EQ(s["player"]["y"].get<int>(), 1);
}

TEST(Protocol, UnknownKeyIsRejectedAndTheGameIsUnchanged) {
    Game g = newGame();
    const json r = call(g, R"({"cmd":"key","key":"Banana"})");
    EXPECT_FALSE(r["ok"].get<bool>());
    EXPECT_NE(r["error"].get<std::string>().find("Banana"), std::string::npos);
    EXPECT_EQ(g.scene(), Scene::MainMenu);
}

TEST(Protocol, KeyCommandNeedsAStringKeyField) {
    Game g = newGame();
    EXPECT_FALSE(call(g, R"({"cmd":"key"})")["ok"].get<bool>());
    EXPECT_FALSE(call(g, R"({"cmd":"key","key":42})")["ok"].get<bool>());
}

TEST(Protocol, GetStateDescribesTheMenu) {
    Game g = newGame();
    const json r = call(g, R"({"cmd":"get_state"})");
    ASSERT_TRUE(r["ok"].get<bool>());
    const json s = r["state"];
    EXPECT_EQ(s["scene"].get<std::string>(), "main_menu");
    EXPECT_EQ(s["score"].get<int>(), 0);
    EXPECT_EQ(s["player"]["x"].get<int>(), 1);
    EXPECT_EQ(s["player"]["y"].get<int>(), 1);
    EXPECT_EQ(s["coins_remaining"].get<int>(), 3);
    EXPECT_EQ(s["width"].get<int>(), 12);
    EXPECT_EQ(s["height"].get<int>(), 9);
}

TEST(Protocol, GetStateShowsPlayingAndPausedScenes) {
    Game g = newGame();
    call(g, R"({"cmd":"key","key":"Enter"})");
    EXPECT_EQ(call(g, R"({"cmd":"get_state"})")["state"]["scene"].get<std::string>(), "playing");
    call(g, R"({"cmd":"key","key":"Escape"})");
    EXPECT_EQ(call(g, R"({"cmd":"get_state"})")["state"]["scene"].get<std::string>(), "paused");
}

TEST(Protocol, QuitRequestsShutdown) {
    Game g = newGame();
    const Reply quit = handleRequest(R"({"cmd":"quit"})", g);
    EXPECT_TRUE(quit.quitRequested);
    EXPECT_TRUE(json::parse(quit.json)["ok"].get<bool>());

    EXPECT_FALSE(handleRequest(R"({"cmd":"ping"})", g).quitRequested);
}

TEST(Protocol, RepliesAreSingleLines) {
    Game g = newGame();
    for (const char* request : {R"({"cmd":"ping"})", R"({"cmd":"get_state"})", "garbage",
                                R"({"cmd":"key","key":"Nope"})"}) {
        EXPECT_EQ(handleRequest(request, g).json.find('\n'), std::string::npos) << request;
    }
}