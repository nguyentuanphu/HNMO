# HNMO server

The game server of HNMO: the login and connect servers, the game servers with
the whole game logic, chat, friends and guilds, and the admin panel to run and
configure it all.

It is played with the HNMO web client, and comes ready to start in the HNMO
folder: server, admin panel, web client and save database in one.

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Documentation

The documentation lives in the [docs-website](docs-website) folder and is built
as a website with [Docusaurus](https://docusaurus.io/). The links below point to
its sources, so they work on GitHub as well.

| | |
|---|---|
| [Getting started](docs-website/docs/getting-started/requirements.md) | Requirements, ports, running with Docker or from source, connecting a game client, test accounts |
| [Deployment](docs-website/docs/deployment/overview.md) | The deployment variants, HTTPS, start parameters and environment variables |
| [Admin panel](docs-website/docs/admin-panel/overview.md) | One page per screen of the server's user interface, plus task-oriented how-tos |
| [Server features](docs-website/docs/server-features/bots.md) | Features with their own configuration, e.g. the server-side AI bots |
| [Development](docs-website/docs/development/architecture.md) | Architecture, solution structure, the plugin system, contributing |
| [Reference](docs-website/docs/reference/ports.md) | Ports and the generated packet documentation |

The code-bound technical documentation stays next to the code: see [docs](docs)
for the packet descriptions and the game mechanics, and the `Readme.md` of the
individual projects under [src](src).

To run the website locally:

```bash
cd docs-website
npm install
npm start
```

## Licensing

This project is released under the MIT license (see LICENSE file).

## Contributions

Read [CONTRIBUTING.md](CONTRIBUTING.md) before you start, and open an issue for
questions or ideas.
