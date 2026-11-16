# Catch-up level & reset cap

The **Catch-up level & reset cap** (off by default) keeps early-season progression fair by holding everyone at an initial level cap (and/or daily reset limit) until the first player reaches it, then increasing today's cap automatically by **N per Sydney day**.

## Configuration (`/catchup`)

1. Enable **Enable Catch-up Cap** when opening a new season or catch-up realm.
2. **Level cap mode**: set **Initial / trigger level** (e.g. `300`), **Levels risen / day** (e.g. `10`), and **Hard max level** (e.g. `400`).
3. **Daily reset cap mode**: set **Initial daily resets**, **+Resets / day**, **Trigger total resets**, and **Hard max / day**. Both in-game `/reset` and website resets enforce the limit automatically.
4. Today's active cap and unlock leader are displayed on `home.html#catchup`.
