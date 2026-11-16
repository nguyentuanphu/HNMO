---
title: Gameplay Rules & Happy Hours
---

# Gameplay Rules & Happy Hours

The **Gameplay Rules & Happy Hours** page (`/gameplay-rules`) controls three server rule plug-ins (all **OFF** by default until enabled):

1. **Per-Map Rules (`MapRulesPlugIn`, 14.1)**:
   - Override PvP on/off, PK penalty exemption, EXP bonus %, Item drop %, and Zen drop % per map number.
2. **Scheduled Bonus Events / Happy Hours (`BonusEventsPlugIn`, 14.2)**:
   - Schedule recurring EXP, item drop, and zen bonus windows evaluated in **Sydney time** (per map or all maps).
   - Broadcasts bilingual (EN/VI) start and end notices in game chat and exposes live schedule entries for the Event Timetable (`GetTimetableEntries`).
3. **Combat Damage Limits (`CombatLimitsPlugIn`, 14.3)**:
   - Configure a maximum damage cap per hit (`0` = unlimited), a PvP damage reduction percentage, and an additional Castle Siege (Valley of Loren, map 30) damage reduction percentage.
