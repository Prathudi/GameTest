#include <gtest/gtest.h>

#include <ostream>
#include <stdexcept>
#include <string>
#include <vector>

#include "gametest/game.h"

namespace gametest {
// Lets gtest print values readably in failure messages.
std::ostream& operator<<(std::ostream& os, const Point& p) {
    return os << "(" << p.x << "," << p.y << ")";
}

std::ostream& operator<<(std::ostream& os, Scene s) {
    switch (s) {
        case Scene::MainMenu: return os << "MainMenu";
        case Scene::Playing: return os << "Playing";
        case Scene::Paused: return os << "Paused";
    }
    return os << "?";
}
}  // namespace gametest

using namespace gametest;
using Level = std::vector<std::string>;

namespace {

// A 3x3 room with the player in the middle at (2,2).
const Level kRoom = {
    "#####",
    "#   #",
    "# @ #",
    "#   #",
    "#####",
};

// A corridor: player at (1,1), one coin at (2,1), floor at (3,1).
const Level kCorridor = {
    "#####",
    "#@o #",
    "#####",
};

// Creates a game and leaves the main menu, so it is in the Playing scene.
Game startedGame(const Level& level) {
    Game g(level);
    g.handleKey(Key::Enter);
    return g;
}

void expectInvalid(const Level& level) {
    EXPECT_THROW(Game g(level), std::invalid_argument);
}

}  // namespace

// ---------- main menu ----------

TEST(Menu, GameStartsInMainMenu) {
    Game g(kRoom);
    EXPECT_EQ(g.scene(), Scene::MainMenu);
    EXPECT_EQ(g.score(), 0);
    EXPECT_EQ(g.player(), (Point{2, 2}));
}

TEST(Menu, EnterStartsTheGame) {
    Game g(kRoom);
    g.handleKey(Key::Enter);
    EXPECT_EQ(g.scene(), Scene::Playing);
}

TEST(Menu, MovementKeysDoNothingInTheMenu) {
    Game g(kRoom);
    g.handleKey(Key::Right);
    EXPECT_EQ(g.scene(), Scene::MainMenu);
    EXPECT_EQ(g.player(), (Point{2, 2}));
}

TEST(Menu, EscapeDoesNotPauseTheMenu) {
    Game g(kRoom);
    g.handleKey(Key::Escape);
    EXPECT_EQ(g.scene(), Scene::MainMenu);
}

// ---------- movement ----------

TEST(Movement, MovesOneCellInEachDirection) {
    Game g = startedGame(kRoom);
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{3, 2}));
    g.handleKey(Key::Down);
    EXPECT_EQ(g.player(), (Point{3, 3}));
    g.handleKey(Key::Left);
    EXPECT_EQ(g.player(), (Point{2, 3}));
    g.handleKey(Key::Up);
    EXPECT_EQ(g.player(), (Point{2, 2}));
}

TEST(Movement, WallBlocksMovement) {
    Game g = startedGame(Level{"###", "#@#", "###"});
    g.handleKey(Key::Up);
    g.handleKey(Key::Down);
    g.handleKey(Key::Left);
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{1, 1}));
}

TEST(Movement, MapEdgeBlocksMovementEvenWithoutWalls) {
    Game g = startedGame(Level{"@ "});
    g.handleKey(Key::Left);
    g.handleKey(Key::Up);
    g.handleKey(Key::Down);
    EXPECT_EQ(g.player(), (Point{0, 0}));
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{1, 0}));
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{1, 0}));
    EXPECT_TRUE(g.isWall({-1, 0}));
}

// ---------- coins and score ----------

TEST(Coins, CollectingACoinIncreasesScore) {
    Game g = startedGame(kCorridor);
    ASSERT_EQ(g.coinsRemaining(), 1);
    ASSERT_TRUE(g.hasCoin({2, 1}));
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{2, 1}));
    EXPECT_EQ(g.score(), 1);
    EXPECT_EQ(g.coinsRemaining(), 0);
    EXPECT_FALSE(g.hasCoin({2, 1}));
}

TEST(Coins, ACoinIsOnlyCollectedOnce) {
    Game g = startedGame(kCorridor);
    g.handleKey(Key::Right);  // collect the coin at (2,1)
    g.handleKey(Key::Right);  // step on to (3,1)
    g.handleKey(Key::Left);   // step back onto (2,1)
    EXPECT_EQ(g.score(), 1);
    EXPECT_EQ(g.player(), (Point{2, 1}));
}

// ---------- pause and resume ----------

TEST(Pause, EscapePausesAndResumes) {
    Game g = startedGame(kRoom);
    g.handleKey(Key::Escape);
    EXPECT_EQ(g.scene(), Scene::Paused);
    g.handleKey(Key::Escape);
    EXPECT_EQ(g.scene(), Scene::Playing);
}

TEST(Pause, PlayerCannotMoveWhilePaused) {
    Game g = startedGame(kRoom);
    g.handleKey(Key::Escape);
    g.handleKey(Key::Right);
    EXPECT_EQ(g.player(), (Point{2, 2}));
}

TEST(Pause, ResumeKeepsPlayerAndScore) {
    Game g = startedGame(kCorridor);
    g.handleKey(Key::Right);  // collect the coin
    g.handleKey(Key::Escape);
    g.handleKey(Key::Escape);
    EXPECT_EQ(g.scene(), Scene::Playing);
    EXPECT_EQ(g.player(), (Point{2, 1}));
    EXPECT_EQ(g.score(), 1);
}

// ---------- level validation ----------

TEST(LevelValidation, RejectsBrokenMaps) {
    expectInvalid(Level{});                      // no rows
    expectInvalid(Level{""});                    // empty row
    expectInvalid(Level{"#@#", "##"});           // rows differ in length
    expectInvalid(Level{"###", "# #", "###"});   // no player
    expectInvalid(Level{"#@@#"});                // two players
    expectInvalid(Level{"#@x#"});                // unknown character
}

TEST(DefaultLevel, IsValid) {
    Game g(Game::defaultLevel());
    EXPECT_EQ(g.scene(), Scene::MainMenu);
    EXPECT_FALSE(g.isWall(g.player()));
    EXPECT_GT(g.coinsRemaining(), 0);
    for (int x = 0; x < g.width(); ++x) {
        EXPECT_TRUE(g.isWall({x, 0}));
        EXPECT_TRUE(g.isWall({x, g.height() - 1}));
    }
    for (int y = 0; y < g.height(); ++y) {
        EXPECT_TRUE(g.isWall({0, y}));
        EXPECT_TRUE(g.isWall({g.width() - 1, y}));
    }
}