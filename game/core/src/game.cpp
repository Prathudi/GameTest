#include "gametest/game.h"

#include <cstddef>
#include <stdexcept>

namespace gametest {

std::vector<std::string> Game::defaultLevel() {
    return {
        "############",
        "#@   #     #",
        "# ## # ### #",
        "#  #   #o  #",
        "## ##### ###",
        "#o   #     #",
        "# ## # ### #",
        "#     o    #",
        "############",
    };
}

Game::Game(const std::vector<std::string>& levelRows) : grid_(levelRows) {
    if (grid_.empty() || grid_[0].empty()) {
        throw std::invalid_argument("level must have at least one row and one column");
    }

    int playerCount = 0;
    for (std::size_t y = 0; y < grid_.size(); ++y) {
        if (grid_[y].size() != grid_[0].size()) {
            throw std::invalid_argument("all level rows must have the same length");
        }
        for (std::size_t x = 0; x < grid_[y].size(); ++x) {
            char& cell = grid_[y][x];
            switch (cell) {
                case '#':
                case ' ':
                    break;
                case 'o':
                    ++coinsRemaining_;
                    break;
                case '@':
                    player_ = {static_cast<int>(x), static_cast<int>(y)};
                    cell = ' ';  // the player is stored separately from the grid
                    ++playerCount;
                    break;
                default:
                    throw std::invalid_argument(std::string("unknown level character: ") + cell);
            }
        }
    }
    if (playerCount != 1) {
        throw std::invalid_argument("level must contain exactly one '@'");
    }
}

void Game::handleKey(Key key) {
    switch (scene_) {
        case Scene::MainMenu:
            if (key == Key::Enter) scene_ = Scene::Playing;
            break;

        case Scene::Playing:
            switch (key) {
                case Key::Escape: scene_ = Scene::Paused; break;
                case Key::Up: tryMove({player_.x, player_.y - 1}); break;
                case Key::Down: tryMove({player_.x, player_.y + 1}); break;
                case Key::Left: tryMove({player_.x - 1, player_.y}); break;
                case Key::Right: tryMove({player_.x + 1, player_.y}); break;
                case Key::Enter: break;
            }
            break;

        case Scene::Paused:
            if (key == Key::Escape) scene_ = Scene::Playing;
            break;
    }
}

void Game::tryMove(Point target) {
    if (isWall(target)) return;  // blocked: the player stays where they are
    player_ = target;
    if (hasCoin(target)) {
        grid_[static_cast<std::size_t>(target.y)][static_cast<std::size_t>(target.x)] = ' ';
        ++score_;
        --coinsRemaining_;
    }
}

bool Game::inBounds(Point p) const {
    return p.x >= 0 && p.y >= 0 && p.x < width() && p.y < height();
}

bool Game::isWall(Point p) const {
    if (!inBounds(p)) return true;
    return grid_[static_cast<std::size_t>(p.y)][static_cast<std::size_t>(p.x)] == '#';
}

bool Game::hasCoin(Point p) const {
    if (!inBounds(p)) return false;
    return grid_[static_cast<std::size_t>(p.y)][static_cast<std::size_t>(p.x)] == 'o';
}

Scene Game::scene() const { return scene_; }
Point Game::player() const { return player_; }
int Game::score() const { return score_; }
int Game::coinsRemaining() const { return coinsRemaining_; }
int Game::width() const { return static_cast<int>(grid_[0].size()); }
int Game::height() const { return static_cast<int>(grid_.size()); }

}  // namespace gametest