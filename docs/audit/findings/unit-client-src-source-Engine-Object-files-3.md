# Unit: client/src/source/Engine/Object (files 3)

Checked against: Season 6/9 client wing/ring option rules & Web `itemValue.ts`.

Scope: `client/src/source/Engine/Object (files 3)` (`3` files, `3064` lines).

## Findings

| File & Line | Rule / Formula | Parity Reference | Result |
|---|---|---|---|
| `ZzzInfomation.cpp:320-380` | 2nd wings exc opts (`50 + Lv*5` HP/MP, `3%` ignore, `50` AG, `+5` speed); Cape of Lord/Fighter opts (`10 + Lv*5` Cmd except Fighter); 3rd wings (`5%` ignore/reflect/fullHP/fullMP) | Season 6/9 wing option table | Same |
| `ZzzInfomation.cpp:384-404` | Wizard's Ring (`+10%` dmg/wiz, `+10` spd); Lethal Wizard's Ring (`+15%` dmg/wiz, `+10` spd) | Season 6/9 special ring stats | Same |
