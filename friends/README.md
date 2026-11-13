# HNMO: everything a friend needs

There are two ways to play. Pick one.

## 1. Play together with the host (no download)

All you need is a web browser and Tailscale (free).

1. Install Tailscale: <https://tailscale.com/download>, and sign in.
2. Ask the host to invite you to their Tailscale network (they send you an invite
   link from the Tailscale admin page, "Share" or "Invite users"). Accept it.
3. The host starts HNMO and sends you their Tailscale address (it looks like
   `100.x.y.z`, shown in the Tailscale app on their computer).
4. Open `http://100.x.y.z:8080/` in your browser (with the host's address).
5. Click **Create one** to make an account (you get free coins), then log in and
   create your character.

Your character lives on the host's computer, so you can only play while the host
has HNMO running.

## 2. Play on your own computer (offline or as a host)

1. Download **[HNMO-windows.zip](https://github.com/nguyentuanphu/HNMO/releases/latest/download/HNMO-windows.zip)**
   (a few hundred MB; this link always gives the newest version).
2. Right-click the zip and choose **Extract All**. You get a folder called `HNMO`.
3. Open it and double-click **Start HNMO.bat**. Keep the window that opens; closing
   it stops the game.
   - If Windows says "Windows protected your PC", click **More info** and
     **Run anyway** (the files aren't signed).
   - The very first start sets up the save game and takes a few minutes.
4. The game opens in your browser at <http://localhost:8080/>. Log in with a test
   account (`test300` / `test300`) or create your own at
   <http://localhost:8080/account.html>.

Nothing else needs to be installed. Your progress is saved in the `save` folder
inside `HNMO`; to back it up, stop the game and copy that folder.

To let others join *your* game, do the same as the host in part 1: invite them to
your Tailscale network and send them your Tailscale address.

The `README.txt` inside the `HNMO` folder explains more: accounts and coins, the
admin panel, and the first-time setup of the account page.

## In the game

- Click to walk, right-drag to turn the camera, mouse wheel to zoom.
- Enter opens the chat; `/move devias` travels to Devias.

## Something doesn't work?

- **The page doesn't load (part 1):** check that Tailscale is connected on both
  computers and that the host's HNMO window is still open.
- **"Wrong password":** the account doesn't exist yet on that game; create it at
  `/account.html` first.
- **The HNMO window says "HNMO couldn't start" or "HNMO stopped":** read the message in it. Usually HNMO is
  already running, or another program uses one of its ports (8080, 44406, 55432);
  close the other one and try again.
