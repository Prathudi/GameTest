#include <cstring>
#include <iostream>

#include "testpilot/agent.h"

int main(int argc, char** argv) {
    if (argc > 1 && std::strcmp(argv[1], "--version") == 0) {
        std::cout << "snake (TestPilot agent " << testpilot::version() << ")\n";
        return 0;
    }
    std::cout << "Snake placeholder - the real game is built in Phase 1.\n";
    return 0;
}
