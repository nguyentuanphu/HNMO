#include "stdafx.h"

#ifdef _EDITOR

#include "SkillEditorPopups.h"
#include "imgui.h"
#include "I18N/All.h"

bool CSkillEditorPopups::RenderSimplePopup(const char* popupId, const char* message)
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

void CSkillEditorPopups::RenderPopups()
{
    // Save popups
    RenderSimplePopup(I18N::Editor::PopupSaveSuccess, I18N::Editor::SkillsSavedSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupSaveFailed, I18N::Editor::FailedToSaveSkills);

    // Legacy Export popups
    RenderSimplePopup(I18N::Editor::PopupExportS6E3Success, I18N::Editor::ExportedAsLegacyFormatSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupExportS6E3Failed, I18N::Editor::FailedToExportAsLegacyFormat);

    // CSV Export popups
    RenderSimplePopup(I18N::Editor::PopupExportCSVSuccess, I18N::Editor::SkillsExportedAsCSVSuccessfully);
    RenderSimplePopup(I18N::Editor::PopupExportCSVFailed, I18N::Editor::FailedToExportSkillAttributesAsCSV);
}

#endif // _EDITOR
