#pragma once

#include "UI/Core/WindowObject.h"
#include "UI/RmlBridge/RmlStackingOrder.h"
#include "UI/RmlBridge/RmlThemedView.h"
#include "UI/Windows/PentagramRmlModel.h"

namespace mu::ui::window
{
class CManager;

class CPentagramWindow : public CObject
{
public:
    static constexpr float LayerDepth = UI::RmlBridge::ForegroundPanelLayerDepth;

    CPentagramWindow();
    ~CPentagramWindow() override;

    bool Create(CManager* pNewUIMng);
    void Release();

    bool UpdateMouseEvent() override;
    bool UpdateKeyEvent() override;
    bool Update() override;
    bool Render() override;

    Rml::ElementDocument* GetPlacedDocument() const override { return m_RmlView.Document(); }
    float GetLayerDepth() override { return LayerDepth; }
    float GetKeyEventOrder() override { return 7.0f; }

    void OpenningProcess();
    void ClosingProcess();

private:
    void BuildRmlUi();
    void SyncRmlModel();
    void BindRmlModel(Rml::DataModelConstructor& c, PentagramRmlModel& model);

    CManager* m_pNewUIMng = nullptr;
    UI::RmlBridge::ThemedView<PentagramRmlModel> m_RmlView{"pentagram_window",
        [this](Rml::DataModelConstructor& c, PentagramRmlModel& model) { BindRmlModel(c, model); },
        {{"Data/Interface/RmlUi/pentagram_window.rml"}}};
    bool m_PendingClose = false;
    int m_PendingAction = 0;
    int m_PendingSlot = 0;
    int m_PendingIndex = 0;
    int m_SelectedSlot = 0;
    int m_RankPairSlot = -1;
};
} // namespace mu::ui::window
