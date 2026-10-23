#include <SFML/Graphics.hpp>

#include <iostream>
#include <optional>
#include <string>

#include "gametest/game.h"
#include "renderer.h"

namespace {

// Translates a real keyboard key into the game's own logical key.
// Keys the game does not use give an empty result.
std::optional<gametest::Key> toGameKey(sf::Keyboard::Key key) {
    switch (key) {
        case sf::Keyboard::Key::Up: return gametest::Key::Up;
        case sf::Keyboard::Key::Down: return gametest::Key::Down;
        case sf::Keyboard::Key::Left: return gametest::Key::Left;
        case sf::Keyboard::Key::Right: return gametest::Key::Right;
        case sf::Keyboard::Key::Enter: return gametest::Key::Enter;
        case sf::Keyboard::Key::Escape: return gametest::Key::Escape;
        default: return std::nullopt;
    }
}

}  // namespace

int main() {
    gametest::Game game(gametest::Game::defaultLevel());

    sf::Font font;
    const std::string fontPath = std::string(GAMETEST_ASSET_DIR) + "/fonts/PressStart2P-Regular.ttf";
    if (!font.openFromFile(fontPath)) {
        std::cerr << "GameTest: could not load the font: " << fontPath << "\n";
        return 1;
    }
    const Renderer renderer(font);

    sf::RenderWindow window(sf::VideoMode(Renderer::windowSize(game)), "GameTest");
    window.setFramerateLimit(60);
    window.setKeyRepeatEnabled(false);  // one key press = one step
    std::cout << "GameTest: running. Enter = start, arrows = move, Esc = pause.\n";

    while (window.isOpen()) {
        while (const std::optional event = window.pollEvent()) {
            if (event->is<sf::Event::Closed>()) {
                window.close();
            } else if (const auto* pressed = event->getIf<sf::Event::KeyPressed>()) {
                if (const auto key = toGameKey(pressed->code)) game.handleKey(*key);
            }
        }

        renderer.draw(window, game);
        window.display();
    }

    std::cout << "GameTest: window closed.\n";
    return 0;
}