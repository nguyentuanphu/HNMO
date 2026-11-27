#pragma once

#include "UI/Core/WindowObject.h"
#include "UI/RmlBridge/RmlStackingOrder.h"
#include "UI/RmlBridge/RmlThemedView.h"
#include "UI/Windows/MuunRmlModel.h"

namespace mu::ui::window
{
class CManager;

class CMuunWindow : public CObject
{
public:
    static constexpr float LayerDepth = UI::RmlBridge::ForegroundPanelLayerDepth;

    CMuunWindow();
    ~CMuunWindow() override;

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
    void BindRmlModel(Rml::DataModelConstructor& c, MuunRmlModel& model);

    CManager* m_pNewUIMng = nullptr;
    UI::RmlBridge::ThemedView<MuunRmlModel> m_RmlView{"muun_window",
        [this](Rml::DataModelConstructor& c, MuunRmlModel& model) { BindRmlModel(c, model); },
        {{"Data/Interface/RmlUi/muun_window.rml"}}};
    bool m_PendingClose = false;
    int m_PendingAction = 0;
    int m_PendingSlot = 0;
};
} // namespace mu::ui::window
