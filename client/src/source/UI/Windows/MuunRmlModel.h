#pragma once

#include <RmlUi/Core/Types.h>

#include <vector>

namespace mu::ui::window
{
struct MuunBagUiRow
{
    int slot = 0;
    Rml::String title;
    Rml::String feedLabel;
    Rml::String convertLabel;
    bool active = false;
    bool canFeed = false;
    bool canConvert = false;

    bool operator==(const MuunBagUiRow&) const = default;
};

struct MuunRmlModel
{
    float textPx = 0.f;
    Rml::String title;
    Rml::String statusNote;
    Rml::String energySummary;
    Rml::String exitTooltip;

    bool hasActive = false;
    Rml::String activeName;
    Rml::String activeBonus;
    Rml::String activeCondition;
    Rml::String levelUpLabel;
    Rml::String evolveLabel;
    bool canLevelUp = false;
    bool canEvolve = false;

    std::vector<MuunBagUiRow> bagRows;
};
} // namespace mu::ui::window
