#include "stdafx.h"
#include "GameLogic/SeasonNine/SeasonNineClientState.h"

#include "Character/CharacterManager.h"
#include "Core/Utilities/StringUtils.h"
#include "Network/Server/WSclient.h"
#include "Render/Effects/ZzzEffect.h"
#include "UI/Core/WindowSystem.h"

#include "json.hpp"

#include <algorithm>
#include <cstring>

namespace GameLogic::SeasonNine
{
namespace
{
using nlohmann::json;

std::string ExtractJsonPayload(const BYTE* data, int32_t size)
{
    if (data == nullptr || size <= 5)
    {
        return {};
    }

    const char* start = reinterpret_cast<const char*>(data + 5);
    const size_t maxLen = static_cast<size_t>(size - 5);
    const void* nul = std::memchr(start, '\0', maxLen);
    const size_t len = nul != nullptr ? static_cast<const char*>(nul) - start : maxLen;
    return std::string(start, len);
}

ErrtelEntry ParseErrtel(const json& obj)
{
    ErrtelEntry entry;
    entry.number = obj.value("number", 0);
    entry.element = obj.value("element", 0);
    if (obj.contains("options") && obj["options"].is_array())
    {
        for (const auto& o : obj["options"])
        {
            ErrtelOptionEntry opt;
            opt.index = o.value("index", 0);
            opt.level = o.value("level", 0);
            opt.value = o.value("value", 0);
            opt.name = o.value("name", std::string{});
            entry.options.push_back(std::move(opt));
        }
    }
    return entry;
}

std::string PentagramResultText(const std::string& result)
{
    if (result == "Ok")
        return "Pentagram action succeeded.";
    if (result == "Disabled")
        return "Pentagram system is disabled.";
    if (result == "InvalidItem")
        return "That item cannot be used here.";
    if (result == "Occupied")
        return "That errtel slot is already occupied.";
    if (result == "Empty")
        return "No pentagram or errtel found.";
    if (result == "ElementMismatch")
        return "The element or rank does not match.";
    if (result == "BagFull")
        return "Inventory is full.";
    if (result == "SaveFailed")
        return "Save failed; nothing changed.";
    if (result == "WrongPlace")
        return "Move to the pentagram mix NPC or a safe zone.";
    if (result == "NotEnoughZen")
        return "Not enough Zen.";
    if (result == "MaximumReached")
        return "Maximum level or rank reached.";
    if (result == "Failed")
        return "The errtel mix failed.";
    if (result == "Broken")
        return "The errtel broke during extraction.";
    return {};
}
} // namespace

SeasonNineClientState& SeasonNineClientState::Instance()
{
    static SeasonNineClientState instance;
    return instance;
}

void SeasonNineClientState::Reset()
{
    m_requested = false;
    m_isSeasonNineRealm = false;
    m_minimapMarkers = true;
    m_muun = {};
    m_pentagram = {};
}

void SeasonNineClientState::RequestAllOnce()
{
    if (m_requested || SocketClient == nullptr)
    {
        return;
    }

    m_requested = true;
    SocketClient->ToGameServer()->SendHnmoSeasonNineClientRequest();
    SocketClient->ToGameServer()->SendHnmoMuunCommand(1, 0);
    SocketClient->ToGameServer()->SendHnmoPentagramCommand(1, 0, 0);
}

bool SeasonNineClientState::HandlePacket(const BYTE* data, int32_t size)
{
    if (data == nullptr || size < 4)
    {
        return false;
    }

    if (data[0] == 0xC1 && size >= 5 && data[2] == 0xF5 && data[3] == 0x5D)
    {
        HandleElementalHit(data, size);
        return true;
    }

    if (data[0] == 0xC2 && size >= 5 && data[3] == 0xF5)
    {
        const BYTE subcode = data[4];
        if (subcode == 0x59)
        {
            HandleClientSettingsJson(ExtractJsonPayload(data, size));
            return true;
        }
        if (subcode == 0x2C)
        {
            HandleMuunJson(ExtractJsonPayload(data, size));
            return true;
        }
        if (subcode == 0x5C)
        {
            HandlePentagramJson(ExtractJsonPayload(data, size));
            return true;
        }
    }

    return false;
}

bool SeasonNineClientState::IsSeasonNineOnlyWorld(int worldIndex)
{
    return worldIndex == 83 || worldIndex == 91 || worldIndex == 92 || (worldIndex >= 94 && worldIndex <= 103);
}

const char* SeasonNineClientState::ElementName(int element)
{
    switch (element)
    {
    case 1:
        return "Fire";
    case 2:
        return "Water";
    case 3:
        return "Earth";
    case 4:
        return "Wind";
    case 5:
        return "Darkness";
    default:
        return "None";
    }
}

std::string SeasonNineClientState::FormatErrtelSummary(const ErrtelEntry& errtel)
{
    std::string out = ElementName(errtel.element);
    for (const auto& o : errtel.options)
    {
        std::string text = o.name;
        const size_t pos = text.find("%d");
        if (pos != std::string::npos)
        {
            text.replace(pos, 2, std::to_string(o.value));
        }
        out += " | Lv." + std::to_string(o.level);
        if (!text.empty())
        {
            out += " " + text;
        }
        else
        {
            out += " +" + std::to_string(o.value);
        }
    }
    return out;
}

void SeasonNineClientState::HandleClientSettingsJson(const std::string& jsonText)
{
    const json parsed = json::parse(jsonText, nullptr, false);
    if (!parsed.is_object())
    {
        return;
    }

    m_isSeasonNineRealm = true;
    m_minimapMarkers = parsed.value("minimap", true);
}

void SeasonNineClientState::HandleMuunJson(const std::string& jsonText)
{
    const json parsed = json::parse(jsonText, nullptr, false);
    if (!parsed.is_object())
    {
        return;
    }

    m_muun.loaded = true;
    m_muun.enabled = parsed.value("enabled", false);
    m_muun.energy = parsed.value("energy", 0);
    m_muun.stones = parsed.value("stones", 0);
    m_muun.converters = parsed.value("converters", 0);
    m_muun.bag.clear();

    if (parsed.contains("active") && parsed["active"].is_object())
    {
        const auto& a = parsed["active"];
        m_muun.hasActive = true;
        m_muun.active.slot = a.value("slot", 0);
        m_muun.active.group = a.value("group", 0);
        m_muun.active.number = a.value("number", 0);
        m_muun.active.name = a.value("name", std::string{});
        m_muun.active.level = a.value("level", 1);
        m_muun.active.maxLevel = a.value("maxLevel", 1);
        m_muun.active.evolved = a.value("evolved", false);
        m_muun.active.bonusEn = a.value("bonusEn", std::string{});
        m_muun.active.bonusVi = a.value("bonusVi", std::string{});
        m_muun.active.bonus = a.value("bonus", 0);
        m_muun.active.applies = a.value("applies", false);
        m_muun.active.conditionEn = a.value("conditionEn", std::string{});
        m_muun.active.conditionVi = a.value("conditionVi", std::string{});
        m_muun.active.canEvolve = a.value("canEvolve", false);
        m_muun.active.levelUpCost = a.value("levelUpCost", 0);
    }
    else
    {
        m_muun.hasActive = false;
        m_muun.active = {};
    }

    if (parsed.contains("bag") && parsed["bag"].is_array())
    {
        for (const auto& item : parsed["bag"])
        {
            MuunBagEntry entry;
            entry.slot = item.value("slot", 0);
            entry.group = item.value("group", 0);
            entry.number = item.value("number", 0);
            entry.name = item.value("name", std::string{});
            entry.level = item.value("level", 1);
            entry.evolved = item.value("evolved", false);
            entry.active = item.value("active", false);
            entry.energy = item.value("energy", 0);
            m_muun.bag.push_back(std::move(entry));
        }
    }

    m_muun.lastMessage.clear();
    if (parsed.contains("event") && parsed["event"].is_object())
    {
        m_muun.lastMessage = parsed["event"].value("textEn", std::string{});
        if (!m_muun.lastMessage.empty() && g_pSystemLogBox != nullptr)
        {
            g_pSystemLogBox->AddText(StringUtils::NarrowToWide(m_muun.lastMessage).c_str(), mu::ui::window::TYPE_SYSTEM_MESSAGE);
        }
    }

    ++m_muun.generation;
}

void SeasonNineClientState::HandlePentagramJson(const std::string& jsonText)
{
    const json parsed = json::parse(jsonText, nullptr, false);
    if (!parsed.is_object())
    {
        return;
    }

    m_pentagram.loaded = true;
    m_pentagram.enabled = parsed.value("enabled", false);
    m_pentagram.items.clear();

    if (parsed.contains("worn") && parsed["worn"].is_object())
    {
        const auto& w = parsed["worn"];
        m_pentagram.hasWorn = true;
        m_pentagram.worn.number = w.value("number", 0);
        m_pentagram.worn.element = w.value("element", 0);
        m_pentagram.worn.usable = w.value("usable", 5);
        m_pentagram.worn.hasErrtel.clear();
        m_pentagram.worn.errtels.clear();
        if (w.contains("errtels") && w["errtels"].is_array())
        {
            for (const auto& slotVal : w["errtels"])
            {
                if (slotVal.is_object())
                {
                    m_pentagram.worn.hasErrtel.push_back(true);
                    m_pentagram.worn.errtels.push_back(ParseErrtel(slotVal));
                }
                else
                {
                    m_pentagram.worn.hasErrtel.push_back(false);
                    m_pentagram.worn.errtels.push_back({});
                }
            }
        }
    }
    else
    {
        m_pentagram.hasWorn = false;
        m_pentagram.worn = {};
    }

    if (parsed.contains("items") && parsed["items"].is_array())
    {
        for (const auto& item : parsed["items"])
        {
            PentagramBagEntry entry;
            entry.slot = item.value("slot", 0);
            entry.number = item.value("number", 0);
            entry.element = item.value("element", 0);
            if (item.contains("errtel") && item["errtel"].is_object())
            {
                entry.isPentagram = false;
                entry.errtel = ParseErrtel(item["errtel"]);
            }
            else
            {
                entry.isPentagram = true;
                entry.usable = item.value("usable", 0);
            }
            m_pentagram.items.push_back(std::move(entry));
        }
    }

    if (parsed.contains("costs") && parsed["costs"].is_object())
    {
        const auto& c = parsed["costs"];
        m_pentagram.levelUpZenPerLevel = c.value("levelUpZenPerLevel", 100000);
        m_pentagram.rankUpZen = c.value("rankUpZen", 1000000);
        m_pentagram.extractZen = c.value("extractZen", 100000);
        m_pentagram.mixNpc = c.value("mixNpc", 0);
    }

    m_pentagram.result = parsed.contains("result") && parsed["result"].is_string()
        ? parsed["result"].get<std::string>()
        : std::string{};
    m_pentagram.lastMessage = PentagramResultText(m_pentagram.result);
    if (!m_pentagram.lastMessage.empty() && g_pSystemLogBox != nullptr)
    {
        g_pSystemLogBox->AddText(StringUtils::NarrowToWide(m_pentagram.lastMessage).c_str(), mu::ui::window::TYPE_SYSTEM_MESSAGE);
    }

    ++m_pentagram.generation;
}

void SeasonNineClientState::HandleElementalHit(const BYTE* data, int32_t size)
{
    if (size < 11)
    {
        return;
    }

    const int targetId = (static_cast<int>(data[4]) << 8) | data[5];
    const uint32_t damage = static_cast<uint32_t>(data[6])
        | (static_cast<uint32_t>(data[7]) << 8)
        | (static_cast<uint32_t>(data[8]) << 16)
        | (static_cast<uint32_t>(data[9]) << 24);
    const BYTE element = data[10];
    if (damage == 0)
    {
        return;
    }

    const int charIdx = FindCharacterIndex(targetId);
    if (charIdx < 0 || charIdx >= MAX_CHARACTERS_CLIENT)
    {
        return;
    }

    vec3_t color = {1.0f, 0.5f, 0.1f};
    switch (element)
    {
    case 1: // Fire
        Vector(1.0f, 0.35f, 0.15f, color);
        break;
    case 2: // Water
        Vector(0.25f, 0.65f, 1.0f, color);
        break;
    case 3: // Earth
        Vector(0.35f, 0.9f, 0.35f, color);
        break;
    case 4: // Wind
        Vector(0.75f, 0.95f, 0.85f, color);
        break;
    case 5: // Darkness
        Vector(0.78f, 0.4f, 0.95f, color);
        break;
    default:
        break;
    }

    CreatePoint(CharactersClient[charIdx].Object.Position, static_cast<int>(damage), color, 15.f, true, false);
}
} // namespace GameLogic::SeasonNine
