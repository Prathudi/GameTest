#include <SFML/Graphics.hpp>

#include <iostream>
#include <optional>

int main() {
    sf::RenderWindow window(sf::VideoMode({640, 480}), "GameTest");
    window.setFramerateLimit(60);
    std::cout << "GameTest: window opened. Press Esc or close the window to quit.\n";

    sf::RectangleShape square({100.f, 100.f});
    square.setPosition({270.f, 190.f});
    square.setFillColor(sf::Color::Green);

    while (window.isOpen()) {
        while (const std::optional event = window.pollEvent()) {
            if (event->is<sf::Event::Closed>()) {
                window.close();
            } else if (const auto* key = event->getIf<sf::Event::KeyPressed>()) {
                if (key->code == sf::Keyboard::Key::Escape) window.close();
            }
        }

        window.clear(sf::Color(30, 30, 40));
        window.draw(square);
        window.display();
    }

    std::cout << "GameTest: window closed.\n";
    return 0;
}