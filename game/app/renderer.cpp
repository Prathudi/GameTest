#include "renderer.h"

namespace {

constexpr float kCell = 48.f;  // size of one grid square, in pixels
constexpr float kHud = 48.f;   // height of the score bar at the top

const sf::Color kBackground(20, 20, 28);
const sf::Color kFloor(38, 38, 52);
const sf::Color kWall(96, 108, 150);
const sf::Color kCoin(255, 205, 40);
const sf::Color kPlayer(80, 220, 120);

// Places text so that its centre is at (cx, cy).
void centerText(sf::Text& text, float cx, float cy) {
    const sf::FloatRect b = text.getLocalBounds();
    text.setOrigin({b.position.x + b.size.x / 2.f, b.position.y + b.size.y / 2.f});
    text.setPosition({cx, cy});
}

}  // namespace

sf::Vector2u Renderer::windowSize(const gametest::Game& game) {
    const float w = static_cast<float>(game.width()) * kCell;
    const float h = static_cast<float>(game.height()) * kCell + kHud;
    return {static_cast<unsigned int>(w), static_cast<unsigned int>(h)};
}

void Renderer::draw(sf::RenderTarget& target, const gametest::Game& game) const {
    target.clear(kBackground);

    // The grid: one square per cell, with a coin on top where there is one.
    sf::RectangleShape tile({kCell - 2.f, kCell - 2.f});
    sf::CircleShape coin(12.f);
    coin.setFillColor(kCoin);
    for (int y = 0; y < game.height(); ++y) {
        for (int x = 0; x < game.width(); ++x) {
            const gametest::Point p{x, y};
            const sf::Vector2f topLeft{static_cast<float>(x) * kCell + 1.f,
                                       kHud + static_cast<float>(y) * kCell + 1.f};
            tile.setPosition(topLeft);
            tile.setFillColor(game.isWall(p) ? kWall : kFloor);
            target.draw(tile);
            if (game.hasCoin(p)) {
                coin.setPosition({topLeft.x + 11.f, topLeft.y + 11.f});
                target.draw(coin);
            }
        }
    }

    // The player.
    const gametest::Point pl = game.player();
    sf::RectangleShape player({kCell - 16.f, kCell - 16.f});
    player.setFillColor(kPlayer);
    player.setPosition({static_cast<float>(pl.x) * kCell + 8.f,
                        kHud + static_cast<float>(pl.y) * kCell + 8.f});
    target.draw(player);

    // The score bar.
    sf::Text hud(font_,
                 "SCORE " + std::to_string(game.score()) + "   COINS LEFT " +
                     std::to_string(game.coinsRemaining()),
                 14);
    hud.setPosition({12.f, 16.f});
    target.draw(hud);

    // Menu and pause screens are drawn on top of the game.
    switch (game.scene()) {
        case gametest::Scene::MainMenu:
            drawOverlay(target, "GAMETEST", "PRESS ENTER TO START");
            break;
        case gametest::Scene::Paused:
            drawOverlay(target, "PAUSED", "PRESS ESC TO RESUME");
            break;
        case gametest::Scene::Playing:
            break;
    }
}

void Renderer::drawOverlay(sf::RenderTarget& target, const std::string& title,
                           const std::string& hint) const {
    const sf::Vector2f size(target.getSize());

    sf::RectangleShape dim(size);
    dim.setFillColor(sf::Color(0, 0, 0, 170));
    target.draw(dim);

    sf::Text titleText(font_, title, 28);
    centerText(titleText, size.x / 2.f, size.y / 2.f - 24.f);
    target.draw(titleText);

    sf::Text hintText(font_, hint, 12);
    centerText(hintText, size.x / 2.f, size.y / 2.f + 24.f);
    target.draw(hintText);
}