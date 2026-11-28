#include "stdafx.h"
#include "UI/Windows/PentagramWindow.h"

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
CPentagramWindow::CPentagramWindow() = default;

CPentagramWindow::~CPentagramWindow()
{
    Release();
}

bool CPentagramWindow::Create(CManager* pNewUIMng)
{
    if (pNewUIMng == nullptr)
    {
        return false;
    }

    m_pNewUIMng = pNewUIMng;
    m_pNewUIMng->AddUIObj(INTERFACE_PENTAGRAM, this);
    BuildRmlUi();
    Show(false);
    return true;
}

void CPentagramWindow::Release()
{
    if (m_pNewUIMng != nullptr)
    {
        m_pNewUIMng->RemoveUIObj(this);
        m_pNewUIMng = nullptr;
    }

    m_RmlView.Release();
}

void CPentagramWindow::OpenningProcess()
{
    m_PendingClose = false;
    m_PendingAction = 0;
    m_PendingSlot = 0;
    m_PendingIndex = 0;
    if (SocketClient != nullptr)
    {
        SocketClient->ToGameServer()->SendHnmoPentagramCommand(1, 0, 0);
    }
}

void CPentagramWindow::ClosingProcess()
{
    m_PendingClose = false;
    m_PendingAction = 0;
}

bool CPentagramWindow::UpdateMouseEvent()
{
    return !IsVisible() || !UI::RmlBridge::IsPointerOver(m_RmlView.Document());
}

bool CPentagramWindow::UpdateKeyEvent()
{
    if (IsVisible() && IsPress(VK_ESCAPE))
    {
        g_pNewUISystem->Hide(INTERFACE_PENTAGRAM);
        PlayBuffer(SOUND_CLICK01);
        return false;
    }

    return true;
}

bool CPentagramWindow::Update()
{
    if (m_PendingClose)
    {
        m_PendingClose = false;
        if (IsVisible())
        {
            g_pNewUISystem->Hide(INTERFACE_PENTAGRAM);
            PlayBuffer(SOUND_CLICK01);
        }
        return true;
    }

    if (m_PendingAction > 0 && SocketClient != nullptr)
    {
        const BYTE action = static_cast<BYTE>(m_PendingAction);
        const BYTE slot = static_cast<BYTE>(m_PendingSlot);
        const BYTE index = static_cast<BYTE>(m_PendingIndex);
        m_PendingAction = 0;
        m_PendingSlot = 0;
        m_PendingIndex = 0;
        SocketClient->ToGameServer()->SendHnmoPentagramCommand(action, slot, index);
    }

    SyncRmlModel();
    return true;
}

bool CPentagramWindow::Render()
{
    return true;
}

void CPentagramWindow::BuildRmlUi()
{
    m_RmlView.Ensure();
}

void CPentagramWindow::BindRmlModel(Rml::DataModelConstructor& c, PentagramRmlModel& model)
{
    c.Bind("text_px", &model.textPx);
    c.Bind("title", &model.title);
    c.Bind("status_note", &model.statusNote);
    c.Bind("costs_summary", &model.costsSummary);
    c.Bind("exit_tooltip", &model.exitTooltip);
    c.Bind("has_worn", &model.hasWorn);
    c.Bind("worn_summary", &model.wornSummary);

    auto slotRow = c.RegisterStruct<PentagramSlotUiRow>();
    slotRow.RegisterMember("index", &PentagramSlotUiRow::index);
    slotRow.RegisterMember("label", &PentagramSlotUiRow::label);
    slotRow.RegisterMember("occupied", &PentagramSlotUiRow::occupied);
    slotRow.RegisterMember("selected", &PentagramSlotUiRow::selected);
    c.RegisterArray<std::vector<PentagramSlotUiRow>>();
    c.Bind("slots", &model.slots);

    auto bagRow = c.RegisterStruct<PentagramBagUiRow>();
    bagRow.RegisterMember("slot", &PentagramBagUiRow::slot);
    bagRow.RegisterMember("is_pentagram", &PentagramBagUiRow::isPentagram);
    bagRow.RegisterMember("title", &PentagramBagUiRow::title);
    bagRow.RegisterMember("can_rank_up", &PentagramBagUiRow::canRankUp);
    bagRow.RegisterMember("selected_pair", &PentagramBagUiRow::selectedPair);
    c.RegisterArray<std::vector<PentagramBagUiRow>>();
    c.Bind("bag_items", &model.bagItems);

    c.BindEventCallback("pentagram_action", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.size() >= 1)
        {
            m_PendingAction = args[0].Get<int>();
            m_PendingSlot = args.size() >= 2 ? args[1].Get<int>() : 0;
            m_PendingIndex = args.size() >= 3 ? args[2].Get<int>() : 0;
        }
    });
    c.BindEventCallback("pentagram_select_slot", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.size() >= 1)
        {
            m_SelectedSlot = args[0].Get<int>();
        }
    });
    c.BindEventCallback("pentagram_attach", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.size() >= 1)
        {
            m_PendingAction = 4;
            m_PendingSlot = args[0].Get<int>();
            m_PendingIndex = m_SelectedSlot;
        }
    });
    c.BindEventCallback("pentagram_rank_pair", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList& args)
    {
        if (args.size() >= 1)
        {
            const int bagSlot = args[0].Get<int>();
            if (m_RankPairSlot < 0)
            {
                m_RankPairSlot = bagSlot;
            }
            else if (m_RankPairSlot == bagSlot)
            {
                m_RankPairSlot = -1;
            }
            else
            {
                m_PendingAction = 7;
                m_PendingSlot = m_RankPairSlot;
                m_PendingIndex = bagSlot;
                m_RankPairSlot = -1;
            }
        }
    });
    c.BindEventCallback("pentagram_exit", [this](Rml::DataModelHandle, Rml::Event&, const Rml::VariantList&)
    {
        m_PendingClose = true;
    });
}

void CPentagramWindow::SyncRmlModel()
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
    const auto& s = GameLogic::SeasonNine::ClientState().Pentagram();

    SyncField(binder, &PentagramRmlModel::title, "title", Rml::String("Pentagram (Ctrl+P)"));
    SyncField(binder, &PentagramRmlModel::exitTooltip, "exit_tooltip",
              StringUtils::WideToNarrow(I18N::Game::Close388));

    if (!s.loaded)
    {
        SyncField(binder, &PentagramRmlModel::statusNote, "status_note", Rml::String("Loading Pentagram..."));
        SyncField(binder, &PentagramRmlModel::costsSummary, "costs_summary", Rml::String{});
        SyncField(binder, &PentagramRmlModel::hasWorn, "has_worn", false);
        SyncField(binder, &PentagramRmlModel::slots, "slots", std::vector<PentagramSlotUiRow>{});
        SyncField(binder, &PentagramRmlModel::bagItems, "bag_items", std::vector<PentagramBagUiRow>{});
        return;
    }

    if (!s.enabled)
    {
        SyncField(binder, &PentagramRmlModel::statusNote, "status_note",
                  Rml::String("Pentagram is disabled outside Season 9."));
        SyncField(binder, &PentagramRmlModel::costsSummary, "costs_summary", Rml::String{});
        SyncField(binder, &PentagramRmlModel::hasWorn, "has_worn", false);
        SyncField(binder, &PentagramRmlModel::slots, "slots", std::vector<PentagramSlotUiRow>{});
        SyncField(binder, &PentagramRmlModel::bagItems, "bag_items", std::vector<PentagramBagUiRow>{});
        return;
    }

    SyncField(binder, &PentagramRmlModel::statusNote, "status_note", s.lastMessage);
    SyncField(
        binder, &PentagramRmlModel::costsSummary, "costs_summary",
        "Rank+ " + std::to_string(s.rankUpZen / 1000) + "k | Ext " + std::to_string(s.extractZen / 1000) + "k");
    SyncField(binder, &PentagramRmlModel::hasWorn, "has_worn", s.hasWorn);

    std::vector<PentagramSlotUiRow> slotRows;
    if (s.hasWorn)
    {
        SyncField(
            binder, &PentagramRmlModel::wornSummary, "worn_summary",
            "Pentagram #12:" + std::to_string(s.worn.number) + " ["
                + GameLogic::SeasonNine::SeasonNineClientState::ElementName(s.worn.element) + "]");
        const int usable = std::clamp(s.worn.usable, 1, 5);
        if (m_SelectedSlot >= usable)
        {
            m_SelectedSlot = 0;
        }

        slotRows.reserve(usable);
        for (int i = 0; i < usable; ++i)
        {
            PentagramSlotUiRow row;
            row.index = i;
            row.selected = (i == m_SelectedSlot);
            row.occupied = i < static_cast<int>(s.worn.hasErrtel.size()) && s.worn.hasErrtel[i];
            if (row.occupied)
            {
                row.label = "Slot " + std::to_string(i + 1) + ": "
                    + GameLogic::SeasonNine::SeasonNineClientState::FormatErrtelSummary(s.worn.errtels[i]);
            }
            else
            {
                row.label = "Slot " + std::to_string(i + 1) + ": [Empty]";
            }
            slotRows.push_back(std::move(row));
        }
    }
    SyncField(binder, &PentagramRmlModel::slots, "slots", std::move(slotRows));

    std::vector<PentagramBagUiRow> bagRows;
    bagRows.reserve(s.items.size());
    for (const auto& item : s.items)
    {
        PentagramBagUiRow row;
        row.slot = item.slot;
        row.isPentagram = item.isPentagram;
        row.selectedPair = (item.slot == m_RankPairSlot);
        if (item.isPentagram)
        {
            row.title = "#" + std::to_string(item.slot) + " 12:" + std::to_string(item.number) + " ["
                + GameLogic::SeasonNine::SeasonNineClientState::ElementName(item.element) + "] ("
                + std::to_string(item.usable) + " slots)";
            row.canRankUp = false;
        }
        else
        {
            row.title = "#" + std::to_string(item.slot) + " Errtel 12:" + std::to_string(item.number) + " "
                + GameLogic::SeasonNine::SeasonNineClientState::FormatErrtelSummary(item.errtel);
            row.canRankUp = !item.errtel.options.empty() && item.errtel.options.size() < 5;
        }
        bagRows.push_back(std::move(row));
    }
    SyncField(binder, &PentagramRmlModel::bagItems, "bag_items", std::move(bagRows));
}
} // namespace mu::ui::window
