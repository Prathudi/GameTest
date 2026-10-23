#pragma once

#include <string>
#include <vector>

namespace gametest {

struct Point {
    int x = 0;
    int y = 0;
    bool operator==(const Point& other) const { return x == other.x && y == other.y; }
};

enum class Scene { MainMenu, Playing, Paused };

enum class Key { Up, Down, Left, Right, Enter, Escape };

// The rules of the demo game. No graphics, no keyboard hardware, no network:
// it only reacts to logical key presses, so tests (and later the test agent)
// can drive it directly.
class Game {
public:
    // Builds a game from a text map. Legend: '#' wall, ' ' floor, 'o' coin,
    // '@' player start (exactly one). Throws std::invalid_argument if the map
    // is malformed.
    explicit Game(const std::vector<std::string>& levelRows);

    // The level the real game uses.
    static std::vector<std::string> defaultLevel();

    // Feed the game one key press.
    void handleKey(Key key);

    // Read-only view of the game state.
    Scene scene() const;
    Point player() const;
    int score() const;
    int coinsRemaining() const;
    int width() const;
    int height() const;
    bool isWall(Point p) const;  // true for walls and for anything outside the map
    bool hasCoin(Point p) const;

private:
    bool inBounds(Point p) const;
    void tryMove(Point target);

    std::vector<std::string> grid_;  // '#', ' ' or 'o'; the player is stored separately
    Point player_;
    Scene scene_ = Scene::MainMenu;
    int score_ = 0;
    int coinsRemaining_ = 0;
};

}  // namespace gametest