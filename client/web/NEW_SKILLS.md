# New skills (server + web only)

All 28 will be built (the owner chose all of them on 2026-11-14). How they're learnt, for every class:

| Skill | How it's learnt |
|---|---|
| A | reaching level 80: learnt by itself, with a message |
| B | reaching level 150: learnt by itself |
| C | reaching level 220: learnt by itself |
| D | the class's trial quest (from level 250): an NPC in Lorencia asks for 100 monsters of a set kind and one jewel; the skill is learnt when it's done (quest reward type Skill) |

A character already past a level when the update arrives learns those skills when it next enters
the game. Skill numbers (the server's and the web's): 700 + 4 × class + letter (A 0, B 1, C 2, D 3),
classes in this order: Dark Knight 0, Dark Wizard 1, Fairy Elf 2, Magic Gladiator 3, Dark Lord 4,
Summoner 5, Rage Fighter 6. So Dark Knight A is 700, Rage Fighter D is 727. They go into existing
saves with a server update plug-in, and into fresh saves with the initializer.

Sounds: generated with ElevenLabs sound effects (one per skill) while the free credits last, then the
game's own sound files. Effects come from the game's own textures and models. Icons come from the
game's own skill icons, recoloured or combined.

The options per class (numbers are a first guess and get balanced in testing):

## Dark Knight
- **A. Earth Splitter**: area around the hero. The sword hits the ground, cracks and rocks spread out, monsters are slowed. Sound: ground slam with a deep boom.
- **B. Blood Slash**: one target, three quick slashes with red trails, heals 5% of the damage. Sound: three fast slashes.
- **C. Iron Guard**: self buff, 20% less damage taken for 15 s, the armour glows gold. Sound: metal ring.
- **D. Charge**: dash up to 5 cells to a monster and stun it for 1 s, with a dust trail. Sound: rush, then impact.

## Dark Wizard
- **A. Ice Dragon**: a dragon of ice flies in a line and slows everything it passes. Sound: ice and wind.
- **B. Meteor Rain**: a 3 x 3 area takes five small meteors over 3 s. Sound: falling fire and explosions.
- **C. Thunder Cage**: a cage of lightning around the target, damage over time. Sound: crackling thunder.
- **D. Mana Shield**: self buff, 30% of the damage taken goes to mana. Sound: magic hum.

## Fairy Elf
- **A. Phoenix Arrow**: a fire arrow that pierces every monster in a line. Sound: bow, then flame.
- **B. Frost Arrow Rain**: arrows of ice rain on an area and slow monsters. Sound: arrows with ice.
- **C. Forest Blessing**: heals the whole party over 10 s, green leaves float around. Sound: soft chime.
- **D. Spirit Wolf**: summons a wolf that fights for 30 s. Sound: howl.

## Magic Gladiator
- **A. Chain Lightning Blade**: a slash that sends lightning jumping to three monsters. Sound: slash, then thunder.
- **B. Flame Whirl**: spins with a burning sword, hitting everything around. Sound: fire and whoosh.
- **C. Twin Power**: self buff, 10% more physical and magic damage for 30 s. Sound: power up.
- **D. Heaven Sword**: swords of light fall on an area. Sound: holy ring and impacts.

## Dark Lord
- **A. Shadow Ravens**: ravens dive at up to five monsters. Sound: raven cries.
- **B. War Command**: party buff, faster attacks for 20 s. Sound: war horn.
- **C. Earth Stomp**: the horse stomps and stuns the area (needs the Dark Horse). Sound: hooves and quake.
- **D. Royal Beam**: a golden beam from the sceptre. Sound: energy beam.

## Summoner
- **A. Poison Soul**: a cloud of poison on an area, damage over time. Sound: hiss.
- **B. Little Demon**: summons a demon that fights for 30 s. Sound: growl.
- **C. Weakening Curse**: the target's defence drops by 20% for 10 s. Sound: dark whisper.
- **D. Soul Drain**: takes mana from the target and gives it to the hero. Sound: drain.

## Rage Fighter
- **A. Dragon Fist**: a punch that sends a dragon of light through a line. Sound: punch and roar.
- **B. Whirl Kicks**: four spinning kicks around the hero. Sound: kicks.
- **C. Iron Body**: self buff, more defence and 10% of the damage reflected. Sound: metal.
- **D. Quake Fist**: punches the ground, a shock wave pushes monsters back. Sound: quake.
