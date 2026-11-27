#include "stdafx.h"
#include "UI/Windows/MuunWindow.h"

#include "Audio/DSPlaySound.h"
#include "Core/Utilities/StringUtils.h"
#include "GameLogic/SeasonNine/SeasonNineClientState.h"
#include "I18N/All.h"
#include "Network/Server/WSclient.h"
#include "UI/Core/WindowManager.h"
#include "UI/Core/WindowSystem.h"
#include "UI/RmlBridge/RmlDocumentVisibility.h"
#include "UI/RmlBridge/RmlNativeTextSize.h"
#include "UI/RmlBridge/RmlPanelGeometry.h"
#include "UI/RmlBridge/RmlPointer.h"
#include "UI/RmlBridge/RmlSyncField.h"

#include <RmlUi/Core/ElementDocument.h>

namespace mu::ui::window
{
CMuunWindow::CMuunWindow() = default;

CMuunWindow::~CMuunWindow()
{
    Release();
}

bool CMuunWindow::Create(CManager* pNewUIMng)
{
    if (pNewUIMng == nullptr)
    {
        return false;
    }

    m_pNewUIMng = pNewUIMng;
    m_pNewUIMng->AddUIObj(INTERFACE_MUUN, this);
    BuildRmlUi();
    Show(false);
    return true;
}

void CMuunWindow::Release()
{
    if (m_pNewUIMng != nullptr)
    {
        m_pNewUIMng->RemoveUIObj(this);
        m_pNewUIMng = nullptr;
    }

    m_RmlView.Release();
}

void CMuunWindow::OpenningProcess()
{
    m_PendingClose = false;
    m_PendingAction = 0;
    m_PendingSlot = 0;
    if (SocketClient != nullptr)
    {
        SocketClient->ToGameServer()->SendHnmoMuunCommand(1, 0);
    }
}

void CMuunWindow::ClosingProcess()
{
    m_PendingClose = false;
    m_PendingAction = 0;
}

bool CMuunWindow::UpdateMouseEvent()
{
    return !IsVisible() || !UI::RmlBridge::IsPointerOver(m_RmlView.Document());
}

bool CMuunWindow::UpdateKeyEvent()
{
    if (IsVisible() && IsPress(VK_ESCAPE))
    {
        g_pNewUISystem->Hide(INTERFACE_MUUN);
        PlayBuffer(SOUND_CLICK01);
        return false;
    }

    return true;
}

bool CMuunWindow::Update()
{
    if (m_PendingClose)
    {
        m_PendingClose = false;
        if (IsVisible())
        {
            g_pNewUISystem->Hide(INTERFACE_MUUN);
            PlayBuffer(SOUND_CLICK01);
        }
        return true;
    }

    if (m_PendingAction > 0 && SocketClient != nullptr)
    {
        const BYTE action = static_cast<BYTE>(m_PendingAction);
        const BYTE slot = static_cast<BYTE>(m_PendingSlot);
        m_PendingAction = 0;
        m_PendingSlot = 0;
        SocketClient->ToGameServer()->SendHnmoMuunCommand(action, slot);
    }

    SyncRmlModel();
    return true;
}

bool CMuunWindow::Render()
{
    return true;
}

void CMuunWindow::BuildRmlUi()
{
    m_RmlView.Ensure();
}

void CMuunWindow::BindRmlModel(Rml::DataModelConstructor& c, MuunRmlModel& model)
{
    c.Bind("text_px", &model.textPx);
    c.Bind("title", &model.title);
    c.Bind("status_note", &model.statusNote);
    c.Bind("energy_summary", &model.energySummary);
    c.Bind("exit_tooltip", &model.exitTooltip);

    c.Bind("has_active", &model.hasActive);
    c.Bind("active_name", &model.activeName);
    c.Bind("active_bonus", &model.activeBonus);
    c.Bind("active_condition", &model.activeCondition);
    c.Bind("level_up_label", &model.levelUpLabel);
    c.Bind("evolve_label", &model.evolveLabel);
    c.Bind("can_level_up", &model.canLevelUp);
    c.Bind("can_evolve", &model.canEvolve);

    auto bagRow = c.RegisterStruct<MuunBagUiRow>();
    bagRow.RegisterMember("slot", &MuunBagUiRow::slot);
    bagRow.RegisterMember("title", &MuunBagUiRow::title);
    bagRow.RegisterMember("feed_label", &MuunBagUiRow::feedLabel);
    bagRow.RegisterMember("convert_label", &MuunBagUiRow::convertLabel);
    bagRow.RegisterMember("active", &MuunBagUiRow::active);
    bagRow.RegisterMember("can_feed", &MuunBagUiRow::canFeed);
    bagRow.RegisterMember("can_convert", &MuunBagUiRow::canConvert);
    c.RegisterArray<std::vector<MuunBagUiRow>>();
    c.Bind("bag_rows", &model.bagRows);

    c.BindEventCallback("muun_action", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.size() >= 1)
        {
            m_PendingAction = args[0].Get<int>();
            m_PendingSlot = args.size() >= 2 ? args[1].Get<int>() : 0;
        }
    });
    c.BindEventCallback("muun_exit", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
    {
        m_PendingClose = true;
    });
}

void CMuunWindow::SyncRmlModel()
{
    BuildRmlUi();
    if (!m_RmlView.Document())
    {
        return;
    }

    UI::RmlBridge::SyncDocumentVisibilityInFront(m_RmlView.Document(), IsVisible());
    if (!IsVisible())
    {
        return;
    }

    UI::RmlBridge::SyncNativeTextSize(m_RmlView.Binder());
    auto& binder = m_RmlView.Binder();
    const auto& s = GameLogic::SeasonNine::ClientState().Muun();

    SyncField(binder, &MuunRmlModel::title, "title", Rml::String("Muun (Alt+N)"));
    SyncField(binder, &MuunRmlModel::exitTooltip, "exit_tooltip",
              StringUtils::WideToNarrow(I18N::Game::Close388));

    if (!s.loaded)
    {
        SyncField(binder, &MuunRmlModel::statusNote, "status_note", Rml::String("Loading Muuns..."));
        SyncField(binder, &MuunRmlModel::energySummary, "energy_summary", Rml::String{});
        SyncField(binder, &MuunRmlModel::hasActive, "has_active", false);
        SyncField(binder, &MuunRmlModel::bagRows, "bag_rows", std::vector<MuunBagUiRow>{});
        return;
    }

    if (!s.enabled)
    {
        SyncField(binder, &MuunRmlModel::statusNote, "status_note",
                  Rml::String("Muuns are disabled outside Season 9."));
        SyncField(binder, &MuunRmlModel::energySummary, "energy_summary", Rml::String{});
        SyncField(binder, &MuunRmlModel::hasActive, "has_active", false);
        SyncField(binder, &MuunRmlModel::bagRows, "bag_rows", std::vector<MuunBagUiRow>{});
        return;
    }

    const Rml::String note = !s.lastMessage.empty()
        ? s.lastMessage
        : (!s.hasActive ? "Pick an active Muun below." : Rml::String{});
    SyncField(binder, &MuunRmlModel::statusNote, "status_note", note);
    SyncField(
        binder, &MuunRmlModel::energySummary, "energy_summary",
        "Energy: " + std::to_string(s.energy) + " | Conv: " + std::to_string(s.converters)
            + " | Stones: " + std::to_string(s.stones));

    SyncField(binder, &MuunRmlModel::hasActive, "has_active", s.hasActive);
    if (s.hasActive)
    {
        const Rml::String lvlText = s.active.evolved
            ? " (Evolved)"
            : (" (Lv." + std::to_string(s.active.level) + "/" + std::to_string(s.active.maxLevel) + ")");
        SyncField(binder, &MuunRmlModel::activeName, "active_name", s.active.name + lvlText);
        SyncField(binder, &MuunRmlModel::activeBonus, "active_bonus", s.active.bonusEn);
        SyncField(
            binder, &MuunRmlModel::activeCondition, "active_condition",
            s.active.conditionEn + (s.active.applies ? " [Active]" : " [Waiting]"));
        SyncField(binder, &MuunRmlModel::canLevelUp, "can_level_up", s.active.levelUpCost > 0);
        SyncField(
            binder, &MuunRmlModel::levelUpLabel, "level_up_label",
            "Lv+ (" + std::to_string(s.active.levelUpCost) + ")");
        SyncField(binder, &MuunRmlModel::canEvolve, "can_evolve", s.active.canEvolve);
        SyncField(
            binder, &MuunRmlModel::evolveLabel, "evolve_label",
            "Evolve (" + std::to_string(s.stones) + ")");
    }

    std::vector<MuunBagUiRow> rows;
    rows.reserve(s.bag.size());
    for (const auto& item : s.bag)
    {
        MuunBagUiRow row;
        row.slot = item.slot;
        row.title = item.name + (item.evolved ? " [E]" : (" Lv." + std::to_string(item.level)));
        row.active = item.active;
        row.canFeed = !item.active && s.hasActive && !s.active.evolved && s.active.level < s.active.maxLevel
            && s.active.group == item.group && s.active.number == item.number;
        row.feedLabel = "Feed";
        row.canConvert = !item.active && s.converters > 0;
        row.convertLabel = "+" + std::to_string(item.energy);
        rows.push_back(std::move(row));
    }
    SyncField(binder, &MuunRmlModel::bagRows, "bag_rows", std::move(rows));
}
} // namespace mu::ui::window
