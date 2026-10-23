#include <gtest/gtest.h>

#include <string>

#include "gametest/agent/line_buffer.h"

using gametest::agent::LineBuffer;

namespace {
void feed(LineBuffer& b, const std::string& text) { b.append(text.data(), text.size()); }
}  // namespace

TEST(LineBuffer, ReturnsNothingUntilALineIsComplete) {
    LineBuffer b;
    EXPECT_FALSE(b.nextLine().has_value());
    feed(b, "no line ending yet");
    EXPECT_FALSE(b.nextLine().has_value());
    EXPECT_EQ(b.pending(), std::string("no line ending yet").size());
}

TEST(LineBuffer, ReassemblesALineSplitAcrossChunks) {
    LineBuffer b;
    feed(b, R"({"cmd":"pi)");
    EXPECT_FALSE(b.nextLine().has_value());
    feed(b, "ng\"}\n");
    const auto line = b.nextLine();
    ASSERT_TRUE(line.has_value());
    EXPECT_EQ(*line, R"({"cmd":"ping"})");
    EXPECT_EQ(b.pending(), 0u);
}

TEST(LineBuffer, SplitsSeveralLinesArrivingTogether) {
    LineBuffer b;
    feed(b, "one\ntwo\nthr");
    EXPECT_EQ(b.nextLine().value(), "one");
    EXPECT_EQ(b.nextLine().value(), "two");
    EXPECT_FALSE(b.nextLine().has_value());  // "thr" is still incomplete
    feed(b, "ee\n");
    EXPECT_EQ(b.nextLine().value(), "three");
}

TEST(LineBuffer, StripsTheCarriageReturnOfWindowsLineEndings) {
    LineBuffer b;
    feed(b, "hello\r\n");
    EXPECT_EQ(b.nextLine().value(), "hello");
}

TEST(LineBuffer, ABlankLineComesBackEmpty) {
    LineBuffer b;
    feed(b, "\n\r\nafter\n");
    EXPECT_EQ(b.nextLine().value(), "");
    EXPECT_EQ(b.nextLine().value(), "");
    EXPECT_EQ(b.nextLine().value(), "after");
}