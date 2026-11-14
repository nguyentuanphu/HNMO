# New skills (server + web only)

All 28 will be built (the owner chose all of them on 2026-11-14). How they're learnt, for every class:

| Skill | How it's learnt |
|---|---|
| A | reaching level 80: learnt by itself, with a message |
| B | reaching level 150: learnt by itself |
| C | reaching level 220: learnt by itself |
| D | the class's trial quest (from level 250): an NPC in Lorencia asks for 200 monsters of a set kind and five Jewels of Chaos; the skill is learnt when it's done (quest reward type Skill) |

A character already past a level when the update arrives learns those skills when it next enters
the game. Skill numbers (the server's and the web's): 700 + 4 × class + letter (A 0, B 1, C 2, D 3),
classes in this order: Dark Knight 0, Dark Wizard 1, Fairy Elf 2, Magic Gladiator 3, Dark Lord 4,
Summoner 5, Rage Fighter 6. So Dark Knight A is 700, Rage Fighter D is 727. They go into existing
saves with a server update plug-in, and into fresh saves with the initializer.

Sounds: made with ElevenLabs sound effects, one per skill (client/web/public/data/Sound/hnmo), the
game's own sound files. Effects come from the game's own textures and models. Icons come from the
game's own skill icons, recoloured or combined.

The options per class. On 2026-11-14 the owner made them 20 % stronger: damage x 1.2 and buffs/debuffs about 20 % stronger; costs and cooldowns stayed. The numbers below are the current ones; the damage of each skill is in `new-skills.json`.

## Dark Knight
- **A. Earth Splitter**: area around the hero. The sword hits the ground, cracks and rocks spread out, monsters are slowed. Sound: ground slam with a deep boom.
  Server: area skill around the hero (range 3); the slow is the ice element (the iced effect, 10 s).
- **B. Blood Slash**: one target, three quick slashes with red trails, heals 6% of the damage. Sound: three fast slashes.
  Server: three hits on one target (explicit-target area skill); a small plug-in heals 6 % of the damage, estimated from the first hit times three.
- **C. Iron Guard**: self buff, 24% less damage taken for 15 s, the armour glows gold. Sound: metal ring.
  Server: self buff, damage received x 0.76 for 15 s (effect 180).
- **D. Charge**: dash up to 5 cells to a monster and stun it for 1 s, with a dust trail. Sound: rush, then impact.
  Server: moves the hero to the target (range 5) and stuns it for 1 s (the stun effect, always).

## Dark Wizard
- **A. Ice Dragon**: a dragon of ice flies in a line and slows everything it passes. Sound: ice and wind.
  Server: a line of 7 cells in front of the hero; the slow is the ice element.
- **B. Meteor Rain**: a 3 x 3 area takes five small meteors over 3 s. Sound: falling fire and explosions.
  Server: up to five hits, 0.6 s apart, on every target within 1.5 cells of the aimed point; fire element.
- **C. Thunder Cage**: a cage of lightning around the target, damage over time. Sound: crackling thunder.
  Server: one target; the damage over time is the poison element (poisoned for 10 s), so it shows as poison.
- **D. Mana Shield**: self buff, 36% of the damage taken goes to mana. Sound: magic hum.
  Server: self buff for 60 s that works like the Soul Barrier: 36 % less damage while each hit costs 2 % of the maximum mana, not exactly 36 % of the damage.

## Fairy Elf
- **A. Phoenix Arrow**: a fire arrow that pierces every monster in a line. Sound: bow, then flame.
  Server: a line of 7 cells, every target in it is hit; fire element.
- **B. Frost Arrow Rain**: arrows of ice rain on an area and slow monsters. Sound: arrows with ice.
  Server: every target within 2 cells of the aimed point; the slow is the ice element.
- **C. Forest Blessing**: heals the whole party over 10 s, green leaves float around. Sound: soft chime.
  Server: party buff for 10 s: health regeneration of 36 + energy x 0.06 per regeneration tick.
- **D. Spirit Wolf**: summons a wolf that fights for 30 s. Sound: howl.
  Server: summons a Splinter Wolf (monster 351); like every summon it stays until it dies or is called back, not 30 s.

## Magic Gladiator
- **A. Chain Lightning Blade**: a slash that sends lightning jumping to three monsters. Sound: slash, then thunder.
  Server: hits the target and two more within 3 cells of it (three hits at most); lightning element, which moves the targets a little.
- **B. Flame Whirl**: spins with a burning sword, hitting everything around. Sound: fire and whoosh.
  Server: area skill around the hero (range 3); fire element.
- **C. Twin Power**: self buff, 12% more physical and magic damage for 30 s. Sound: power up.
  Server: self buff, all damage x 1.12 for 30 s (effect 183).
- **D. Heaven Sword**: swords of light fall on an area. Sound: holy ring and impacts.
  Server: every target within 2 cells of the aimed point.

## Dark Lord
- **A. Shadow Ravens**: ravens dive at up to five monsters. Sound: raven cries.
  Server: the target and others within 3 cells of it, five hits at most.
- **B. War Command**: party buff, faster attacks for 20 s. Sound: war horn.
  Server: party buff, attack and magic speed +24 for 20 s (effect 184).
- **C. Earth Stomp**: the horse stomps and stuns the area (needs the Dark Horse). Sound: hooves and quake.
  Server: area skill around the hero (range 3), stuns for 2 s; it can only be used with the Dark Horse.
- **D. Royal Beam**: a golden beam from the sceptre. Sound: energy beam.
  Server: a line of 7 cells in front of the hero.

## Summoner
- **A. Poison Soul**: a cloud of poison on an area, damage over time. Sound: hiss.
  Server: every target within 2 cells of the aimed point; the damage over time is the poison element.
- **B. Little Demon**: summons a demon that fights for 30 s. Sound: growl.
  Server: summons a Devil (monster 37); it stays until it dies or is called back, not 30 s.
- **C. Weakening Curse**: the target's defence drops by 24% for 10 s. Sound: dark whisper.
  Server: the target's defence x 0.76 for 10 s (effect 186).
- **D. Soul Drain**: takes mana from the target and gives it to the hero. Sound: drain.
  Server: one target; a small plug-in gives the hero 12 % of the damage as mana, taken from the target's mana when it's a player (monsters have none).

## Rage Fighter
- **A. Dragon Fist**: a punch that sends a dragon of light through a line. Sound: punch and roar.
  Server: a line of 5 cells in front of the hero.
- **B. Whirl Kicks**: four spinning kicks around the hero. Sound: kicks.
  Server: four hits on every target around the hero (range 2).
- **C. Iron Body**: self buff, more defence and 12% of the damage reflected. Sound: metal.
  Server: self buff for 30 s: defence x 1.24 and 12 % damage reflection (effect 185).
- **D. Quake Fist**: punches the ground, a shock wave pushes monsters back. Sound: quake.
  Server: area skill around the hero (range 3); the push back is the lightning element, which moves the targets a random step, not straight away from the hero.

## Server

Built in the server; the list for the web client is `client/web/public/data/new-skills.json` (a server
test checks it against the definitions).

- The skills, their effects and the trial quests are created by `HnmoSkillsInitializer` and
  `HnmoTrialQuestsInitializer` (`server/src/Persistence/Initialization/Skills`): for new saves by the
  data initialization, for existing saves by the mandatory update "Add the HNMO skills".
- A, B and C: the plug-in "Learn skills by level" teaches them at 80, 150 and 220 (the list is its
  configuration in the admin panel), and when a character enters the game already past the level,
  with the message "You have learned <skill>.". Every skill also requires its level to be used.
- D: the Elf Soldier (257) in Lorencia gives the trial in the quest dialog (quest group 40, one quest
  per class evolution, from level 250, not repeatable): kill 200 monsters and bring five Jewels of Chaos.
  Monsters: Dark Knight Iron Rider (352), Dark Wizard Satyros (353), Fairy Elf Blade Hunter (354),
  Magic Gladiator Kentauros (355), Dark Lord Gigantis (356), Summoner Death Rider (306, Aida), Rage
  Fighter Ice Walker (454, Raklion). The reward is the class's D skill.
- Cooldowns: the server has no skill cooldowns. `cooldownMs` in the list is for the web client only;
  the server doesn't enforce it.
- Effects 180 to 186 are new effect numbers (Iron Guard, Mana Shield, Forest Blessing, Twin Power,
  War Command, Iron Body, Weakening Curse); Charge and Earth Stomp use the stun effect (61), the ice
  skills the iced effect (56), the poison ones the poisoned effect (55).
- Values: `HnmoSkillValues` (`server/src/Persistence/Initialization/Skills`) holds the damage, the effect
  strengths and the trial's 200 kills / five jewels. Existing saves got the stronger values from the mandatory
  update "Make the HNMO skills stronger" (2026-11-14), which sets them in place.
- Damage (x 1.2, 2026-11-14): Earth Splitter 108, Blood Slash 72, Charge 144, Ice Dragon 90, Meteor Rain 48,
  Thunder Cage 72, Phoenix Arrow 84, Frost Arrow Rain 66, Chain Lightning Blade 96, Flame Whirl 120,
  Heaven Sword 156, Shadow Ravens 102, Earth Stomp 132, Royal Beam 192, Poison Soul 60, Soul Drain 84,
  Dragon Fist 108, Whirl Kicks 36, Quake Fist 180. The summons (Spirit Wolf, Little Demon) are the monsters'
  own strength.
- Admin panel: Accounts, "HNMO skills" grants or removes any of these skills of a character's class (the D skill
  without its trial). A character in the game gets it at once; an offline one at its next login.
