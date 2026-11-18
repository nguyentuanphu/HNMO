---
title: Event & Boss Timetable
sidebar_position: 14
description: Configure the live event, invasion and world boss timetable plus extra scheduled invasions (golden monsters, white wizard, rabbits, snowmen, find the GM).
---

# Event & Boss Timetable

**Navigation:** *Rules and events → Event & Boss Timetable* — route `/timetable`

## Overview

When enabled (OFF by default), players can view every scheduled mini-game (Blood Castle, Devil Square, Chaos Castle, Illusion Temple, Kanturu), scheduled invasion, world boss, and custom server event with:

- Next start time in **Sydney time (`Australia/Sydney`)** and the player's own local time
- Live active indicator and countdown timer
- Spawn map(s) and item/coin drops summary
- Website display under **Schedule** (`home.html`) and in-game window via **H** or the **(i)** guide

## Extra scheduled invasions

You can toggle and configure daily `HH:mm` times directly on `/timetable`:

- **Golden Invasion** & **Red Dragon Invasion** & **White Wizard Corps**
- **Lunar Rabbit Invasion** (`RabbitInvasionPlugIn`, OFF by default)
- **Snowman Invasion** (`SnowmanInvasionPlugIn`, OFF by default — converts Transformed Snowman & Cursed Santa into combat monsters)
- **Find the GM** (`FindTheGmInvasionPlugIn`, OFF by default — spawns a hidden GameMaster NPC on a map; the first player(s) to talk to it win configured coins and zen, recorded in `save/hnmo-state.json`)
