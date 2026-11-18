#include "stdafx.h"

#ifdef _EDITOR

#include "ItemEditorPopups.h"
#include "imgui.h"
#include "I18N/All.h"

bool CItemEditorPopups::RenderSimplePopup(const char* popupId, const char* message)
{
    if (ImGui::BeginPopupModal(popupId, NULL, ImGuiWindowFlags_AlwaysAutoResize))
    {
        ImGui::Text("%s", message);
        if (ImGui::Button(I18N::Editor::OK, ImVec2(120, 0)))
        {
            ImGui::CloseCurrentPopup();
        }
        ImGui::EndPopup();
        return true;
    }
    return false;
}

void CItemEditorPopups::RenderAll()
{
    // Save popups
    RenderSimplePopup(I18N::Editor::PopupSaveSuccess, I18N::Editor::ItemsSavedSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupSaveFailed, I18N::Editor::ItemDataHasFatalProblems);
    RenderSimplePopup(I18N::Editor::PopupSaveWriteFailed, I18N::Editor::ItemDataCouldNotBeWritten);

    // CSV Export popups
    RenderSimplePopup(I18N::Editor::PopupExportCSVSuccess, I18N::Editor::ItemsExportedAsCSVSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupExportCSVFailed, I18N::Editor::FailedToExportItemAttributesAsCSV);

    // bmd import/export popups
    RenderSimplePopup(I18N::Editor::PopupImportBmdSuccess, I18N::Editor::ItemsImportedFromBmd);
    RenderSimplePopup(I18N::Editor::PopupImportBmdFailed, I18N::Editor::FailedToImportItemsFromBmd);
    RenderSimplePopup(I18N::Editor::PopupExportBmdSuccess, I18N::Editor::ItemsExportedAsBmd);
    RenderSimplePopup(I18N::Editor::PopupExportBmdFailed, I18N::Editor::FailedToExportItemsAsBmd);

    // S6E3 Export popups
    RenderSimplePopup(I18N::Editor::PopupExportS6E3Success, I18N::Editor::ExportedAsLegacyFormatSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupExportS6E3Failed, I18N::Editor::FailedToExportAsLegacyFormat);
}

#endif // _EDITOR
