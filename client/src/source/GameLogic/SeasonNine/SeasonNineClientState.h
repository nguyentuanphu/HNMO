#pragma once

#include "stdafx.h"

#include <cstdint>
#include <string>
#include <vector>

namespace GameLogic::SeasonNine
{
struct MuunActiveEntry
{
    int slot = 0;
    int group = 0;
    int number = 0;
    std::string name;
    int level = 1;
    int maxLevel = 1;
    bool evolved = false;
    std::string bonusEn;
    std::string bonusVi;
    int bonus = 0;
    bool applies = false;
    std::string conditionEn;
    std::string conditionVi;
    bool canEvolve = false;
    int levelUpCost = 0;
};

struct MuunBagEntry
{
    int slot = 0;
    int group = 0;
    int number = 0;
    std::string name;
    int level = 1;
    bool evolved = false;
    bool active = false;
    int energy = 0;
};

struct MuunStateData
{
    bool loaded = false;
    bool enabled = false;
    bool hasActive = false;
    MuunActiveEntry active;
    std::vector<MuunBagEntry> bag;
    int energy = 0;
    int stones = 0;
    int converters = 0;
    std::string lastMessage;
    uint64_t generation = 0;
};

struct ErrtelOptionEntry
{
    int index = 0;
    int level = 0;
    int value = 0;
    std::string name;
};

struct ErrtelEntry
{
    int number = 0;
    int element = 0;
    std::vector<ErrtelOptionEntry> options;
};

struct PentagramWornEntry
{
    int number = 0;
    int element = 0;
    int usable = 5;
    std::vector<bool> hasErrtel;
    std::vector<ErrtelEntry> errtels;
};

struct PentagramBagEntry
{
    int slot = 0;
    int number = 0;
    int element = 0;
    bool isPentagram = false;
    int usable = 0;
    ErrtelEntry errtel;
};

struct PentagramStateData
{
    bool loaded = false;
    bool enabled = false;
    bool hasWorn = false;
    PentagramWornEntry worn;
    std::vector<PentagramBagEntry> items;
    int levelUpZenPerLevel = 100000;
    int rankUpZen = 1000000;
    int extractZen = 100000;
    int mixNpc = 0;
    std::string result;
    std::string lastMessage;
    uint64_t generation = 0;
};

class SeasonNineClientState
{
public:
    static SeasonNineClientState& Instance();

    void Reset();
    void RequestAllOnce();
    bool HandlePacket(const BYTE* data, int32_t size);

    bool MinimapMarkersEnabled() const { return m_minimapMarkers; }
    bool IsSeasonNineRealm() const { return m_isSeasonNineRealm; }
    const MuunStateData& Muun() const { return m_muun; }
    const PentagramStateData& Pentagram() const { return m_pentagram; }

    static bool IsSeasonNineOnlyWorld(int worldIndex);
    static const char* ElementName(int element);
    static std::string FormatErrtelSummary(const ErrtelEntry& errtel);

private:
    SeasonNineClientState() = default;

    void HandleClientSettingsJson(const std::string& jsonText);
    void HandleMuunJson(const std::string& jsonText);
    void HandlePentagramJson(const std::string& jsonText);
    void HandleElementalHit(const BYTE* data, int32_t size);

    bool m_requested = false;
    bool m_isSeasonNineRealm = false;
    bool m_minimapMarkers = true;
    MuunStateData m_muun;
    PentagramStateData m_pentagram;
};

inline SeasonNineClientState& ClientState()
{
    return SeasonNineClientState::Instance();
}
} // namespace GameLogic::SeasonNine
