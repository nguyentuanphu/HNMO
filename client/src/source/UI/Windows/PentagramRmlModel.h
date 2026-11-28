#pragma once

#include <RmlUi/Core/Types.h>

#include <vector>

namespace mu::ui::window
{
struct PentagramSlotUiRow
{
    int index = 0;
    Rml::String label;
    bool occupied = false;
    bool selected = false;

    bool operator==(const PentagramSlotUiRow&) const = default;
};

struct PentagramBagUiRow
{
    int slot = 0;
    bool isPentagram = false;
    Rml::String title;
    bool canRankUp = false;
    bool selectedPair = false;

    bool operator==(const PentagramBagUiRow&) const = default;
};

struct PentagramRmlModel
{
    float textPx = 0.f;
    Rml::String title;
    Rml::String statusNote;
    Rml::String costsSummary;
    Rml::String exitTooltip;

    bool hasWorn = false;
    Rml::String wornSummary;
    std::vector<PentagramSlotUiRow> slots;
    std::vector<PentagramBagUiRow> bagItems;
};
} // namespace mu::ui::window
