// A standalone check of the generated editor catalogs; compile with the generated I18N sources.
#include "All.h"
#include "Editor.h"
#include "Metadata.h"
#include <cassert>
#include <cstring>
#include <iostream>

int main()
{
    I18N::SetLocale("vi");
    assert(std::strcmp(I18N::GetCurrentLocale(), "vi") == 0);
    assert(std::strcmp(I18N::GetLanguageDisplayName("vi"), "Tiếng Việt") == 0);
    assert(std::strcmp(I18N::Editor::SaveSkills, "Lưu kỹ năng") == 0);
    assert(std::strcmp(I18N::Editor::OpenEditor, "Mở trình biên tập") == 0);
    assert(std::strcmp(I18N::Editor::PopupSaveSuccess, "Lưu thành công") == 0);
    assert(std::strcmp(I18N::Metadata::Name, "Tên") == 0);
    assert(std::strcmp(I18N::Metadata::MinDamage, "Sát thương tối thiểu") == 0);
    assert(I18N::Format(I18N::Editor::MapImportSelected, {"12"}).find("12") != std::string::npos);
    I18N::SetLocale("en");
    assert(std::strcmp(I18N::Editor::SaveSkills, "Save Skills") == 0);
    assert(std::strcmp(I18N::Editor::PopupSaveSuccess, "Save Success") == 0);
    std::cout << "Compiled locale dispatch, Vietnamese labels, format substitution and English restoration passed.\n";
}
