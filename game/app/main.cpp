#include <SFML/Graphics.hpp>

#include <iostream>
#include <optional>
#include <string>

#include "gametest/agent/agent_server.h"
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

// Reads a TCP port number (0-65535) from text. Gives an empty result if it is not one.
std::optional<unsigned short> parsePort(const std::string& text) {
    try {
        std::size_t used = 0;
        const int value = std::stoi(text, &used);
        if (used != text.size() || value < 0 || value > 65535) return std::nullopt;
        return static_cast<unsigned short>(value);
    } catch (const std::exception&) {
        return std::nullopt;
    }
}

}  // namespace

int main(int argc, char** argv) {
    std::optional<unsigned short> agentPort;
    for (int i = 1; i < argc; ++i) {
        const std::string arg = argv[i];
        if (arg == "--agent-port" && i + 1 < argc) {
            agentPort = parsePort(argv[++i]);
            if (!agentPort) {
                std::cerr << "GameTest: invalid port: " << argv[i] << "\n";
                return 1;
            }
        } else {
            std::cerr << "GameTest: unknown argument: " << arg << "\n"
                      << "usage: game [--agent-port N]\n";
            return 1;
        }
    }

    gametest::Game game(gametest::Game::defaultLevel());

    sf::Font font;
    const std::string fontPath = std::string(GAMETEST_ASSET_DIR) + "/fonts/PressStart2P-Regular.ttf";
    if (!font.openFromFile(fontPath)) {
        std::cerr << "GameTest: could not load the font: " << fontPath << "\n";
        return 1;
    }
    const Renderer renderer(font);

    // The test agent is optional: it only starts when --agent-port is given.
    gametest::agent::AgentServer agent;
    if (agentPort) {
        if (!agent.start(*agentPort)) {
            std::cerr << "GameTest: could not listen on 127.0.0.1:" << *agentPort << "\n";
            return 1;
        }
        std::cout << "GameTest: agent listening on 127.0.0.1:" << agent.port() << std::endl;
    }

    sf::RenderWindow window(sf::VideoMode(Renderer::windowSize(game)), "GameTest");
    window.setFramerateLimit(60);
    window.setKeyRepeatEnabled(false);  // one key press = one step
    std::cout << "GameTest: running. Enter = start, arrows = move, Esc = pause." << std::endl;

    while (window.isOpen()) {
        while (const std::optional event = window.pollEvent()) {
            if (event->is<sf::Event::Closed>()) {
                window.close();
            } else if (const auto* pressed = event->getIf<sf::Event::KeyPressed>()) {
                if (const auto key = toGameKey(pressed->code)) game.handleKey(*key);
            }
        }

        // Frame boundary: run the requests the agent has received since last frame.
        if (agent.processPending(game)) window.close();  // a client sent "quit"

        renderer.draw(window, game);
        window.display();
    }

    std::cout << "GameTest: window closed." << std::endl;
    return 0;
}