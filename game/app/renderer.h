#pragma once

#include <SFML/Graphics.hpp>

#include <string>

#include "gametest/game.h"

// Draws the game state. It only reads the Game and never changes it.
class Renderer {
public:
    explicit Renderer(const sf::Font& font) : font_(font) {}

    // Window size (in pixels) needed to show the whole level plus the score bar.
    static sf::Vector2u windowSize(const gametest::Game& game);

    // Draws one complete frame. Works with any render target, so the same code
    // can draw to the window now and to an off-screen image later.
    void draw(sf::RenderTarget& target, const gametest::Game& game) const;

private:
    void drawOverlay(sf::RenderTarget& target, const std::string& title,
                     const std::string& hint) const;

    const sf::Font& font_;
};