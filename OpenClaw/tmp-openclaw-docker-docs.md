# Docker - OpenClaw

**Source**: https://docs.openclaw.ai/install/docker

---

Skip to main content

[OpenClaw home page![light logo](https://mintcdn.com/clawdhub/dpADRo8IUoiDztzJ/assets/pixel-lobster.svg?fit=max&auto=format&n=dpADRo8IUoiDztzJ&q=85&s=8fdf719fb6d3eaad7c65231385bf28e5)![dark logo](https://mintcdn.com/clawdhub/dpADRo8IUoiDztzJ/assets/pixel-lobster.svg?fit=max&auto=format&n=dpADRo8IUoiDztzJ&q=85&s=8fdf719fb6d3eaad7c65231385bf28e5)](https://docs.openclaw.ai/install/</>)

![US](https://d3gk2c5xim1je2.cloudfront.net/flags/US.svg)

English

Search...

⌘K

  * [GitHub](https://docs.openclaw.ai/install/<https:/github.com/openclaw/openclaw>)
  * [Releases](https://docs.openclaw.ai/install/<https:/github.com/openclaw/openclaw/releases>)

Search...

Navigation

Other install methods

Docker

[Get started](https://docs.openclaw.ai/install/</>)[Install](https://docs.openclaw.ai/install/</install>)[Channels](https://docs.openclaw.ai/install/</channels>)[Agents](https://docs.openclaw.ai/install/</pi>)[Tools](https://docs.openclaw.ai/install/</tools>)[Models](https://docs.openclaw.ai/install/</providers>)[Platforms](https://docs.openclaw.ai/install/</platforms>)[Gateway & Ops](https://docs.openclaw.ai/install/</gateway>)[Reference](https://docs.openclaw.ai/install/</cli>)[Help](https://docs.openclaw.ai/install/</help>)

##### Install overview

  * [Install](https://docs.openclaw.ai/install/</install>)
  * [Installer Internals](https://docs.openclaw.ai/install/</install/installer>)

##### Other install methods

  * [Docker](https://docs.openclaw.ai/install/</install/docker>)
  * [Podman](https://docs.openclaw.ai/install/</install/podman>)
  * [Nix](https://docs.openclaw.ai/install/</install/nix>)
  * [Ansible](https://docs.openclaw.ai/install/</install/ansible>)
  * [Bun (Experimental)](https://docs.openclaw.ai/install/</install/bun>)

##### Maintenance

  * [Updating](https://docs.openclaw.ai/install/</install/updating>)
  * [Migration Guide](https://docs.openclaw.ai/install/</install/migrating>)
  * [Uninstall](https://docs.openclaw.ai/install/</install/uninstall>)

##### Hosting and deployment

  * [VPS Hosting](https://docs.openclaw.ai/install/</vps>)
  * [Kubernetes](https://docs.openclaw.ai/install/</install/kubernetes>)
  * [Fly.io](https://docs.openclaw.ai/install/</install/fly>)
  * [Hetzner](https://docs.openclaw.ai/install/</install/hetzner>)
  * [GCP](https://docs.openclaw.ai/install/</install/gcp>)
  * [macOS VMs](https://docs.openclaw.ai/install/</install/macos-vm>)
  * [exe.dev](https://docs.openclaw.ai/install/</install/exe-dev>)
  * [Deploy on Railway](https://docs.openclaw.ai/install/</install/railway>)
  * [Deploy on Render](https://docs.openclaw.ai/install/</install/render>)
  * [Deploy on Northflank](https://docs.openclaw.ai/install/</install/northflank>)

##### Advanced

  * [Development Channels](https://docs.openclaw.ai/install/</install/development-channels>)

On this page

  * Docker (optional)
  * Is Docker right for me?
  * Requirements
  * Containerized Gateway (Docker Compose)
  * Quick start (recommended)
  * Enable agent sandbox for Docker gateway (opt-in)
  * Automation/CI (non-interactive, no TTY noise)
  * Shared-network security note (CLI + gateway)
  * Use a remote image (skip local build)
  * Base image metadata
  * Shell Helpers (optional)
  * Manual flow (compose)
  * Control UI token + pairing (Docker)
  * Extra mounts (optional)
  * Persist the entire container home (optional)
  * Install extra apt packages (optional)
  * Pre-install extension dependencies (optional)
  * Power-user / full-featured container (opt-in)
  * Permissions + EACCES
  * Faster rebuilds (recommended)
  * Channel setup (optional)
  * OpenAI Codex OAuth (headless Docker)
  * Health checks
  * E2E smoke test (Docker)
  * QR import smoke test (Docker)
  * LAN vs loopback (Docker Compose)
  * Notes
  * Storage model
  * Agent Sandbox (host gateway + Docker tools)
  * What it does
  * Per-agent sandbox profiles (multi-agent)
  * Default behavior
  * Enable sandboxing
  * Build the default sandbox image
  * Sandbox common image (optional)
  * Sandbox browser image
  * Custom sandbox image
  * Tool policy (allow/deny)
  * Pruning strategy
  * Security notes
  * Troubleshooting

Other install methods

# Docker

# 

​

Docker (optional)

Docker is **optional**. Use it only if you want a containerized gateway or to validate the Docker flow.

## 

​

Is Docker right for me?

  * **Yes** : you want an isolated, throwaway gateway environment or to run OpenClaw on a host without local installs.
  * **No** : you’re running on your own machine and just want the fastest dev loop. Use the normal install flow instead.
  * **Sandboxing note** : agent sandboxing uses Docker too, but it does **not** require the full gateway to run in Docker. See [Sandboxing](https://docs.openclaw.ai/install/</gateway/sandboxing>).

This guide covers:

  * Containerized Gateway (full OpenClaw in Docker)
  * Per-session Agent Sandbox (host gateway + Docker-isolated agent tools)

Sandboxing details: [Sandboxing](https://docs.openclaw.ai/install/</gateway/sandboxing>)

## 

​

Requirements

  * Docker Desktop (or Docker Engine) + Docker Compose v2
  * At least 2 GB RAM for image build (`pnpm install` may be OOM-killed on 1 GB hosts with exit 137)
  * Enough disk for images + logs
  * If running on a VPS/public host, review [Security hardening for network exposure](https://docs.openclaw.ai/install/</gateway/security#04-network-exposure-bind--port--firewall>), especially Docker `DOCKER-USER` firewall policy.

## 

​

Containerized Gateway (Docker Compose)

### 

​

Quick start (recommended)

Docker defaults here assume bind modes (`lan`/`loopback`), not host aliases. Use bind mode values in `gateway.bind` (for example `lan` or `loopback`), not host aliases like `0.0.0.0` or `localhost`.

From repo root:

Copy
[code]
    ./docker-setup.sh
    
[/code]

This script:

  * builds the gateway image locally (or pulls a remote image if `OPENCLAW_IMAGE` is set)
  * runs onboarding
  * prints optional provider setup hints
  * starts the gateway via Docker Compose
  * generates a gateway token and writes it to `.env`

Optional env vars:

  * `OPENCLAW_IMAGE` — use a remote image instead of building locally (e.g. `ghcr.io/openclaw/openclaw:latest`)
  * `OPENCLAW_DOCKER_APT_PACKAGES` — install extra apt packages during build
  * `OPENCLAW_EXTENSIONS` — pre-install extension dependencies at build time (space-separated extension names, e.g. `diagnostics-otel matrix`)
  * `OPENCLAW_EXTRA_MOUNTS` — add extra host bind mounts
  * `OPENCLAW_HOME_VOLUME` — persist `/home/node` in a named volume
  * `OPENCLAW_SANDBOX` — opt in to Docker gateway sandbox bootstrap. Only explicit truthy values enable it: `1`, `true`, `yes`, `on`
  * `OPENCLAW_INSTALL_DOCKER_CLI` — build arg passthrough for local image builds (`1` installs Docker CLI in the image). `docker-setup.sh` sets this automatically when `OPENCLAW_SANDBOX=1` for local builds.
  * `OPENCLAW_DOCKER_SOCKET` — override Docker socket path (default: `DOCKER_HOST=unix://...` path, else `/var/run/docker.sock`)
  * `OPENCLAW_ALLOW_INSECURE_PRIVATE_WS=1` — break-glass: allow trusted private-network `ws://` targets for CLI/onboarding client paths (default is loopback-only)
  * `OPENCLAW_BROWSER_DISABLE_GRAPHICS_FLAGS=0` — disable container browser hardening flags `--disable-3d-apis`, `--disable-software-rasterizer`, `--disable-gpu` when you need WebGL/3D compatibility.
  * `OPENCLAW_BROWSER_DISABLE_EXTENSIONS=0` — keep extensions enabled when browser flows require them (default keeps extensions disabled in sandbox browser).
  * `OPENCLAW_BROWSER_RENDERER_PROCESS_LIMIT=<N>` — set Chromium renderer process limit; set to `0` to skip the flag and use Chromium default behavior.

After it finishes:

  * Open `http://127.0.0.1:18789/` in your browser.
  * Paste the token into the Control UI (Settings → token).
  * Need the URL again? Run `docker compose run --rm openclaw-cli dashboard --no-open`.

### 

​

Enable agent sandbox for Docker gateway (opt-in)

`docker-setup.sh` can also bootstrap `agents.defaults.sandbox.*` for Docker deployments. Enable with:

Copy
[code]
    export OPENCLAW_SANDBOX=1
    ./docker-setup.sh
    
[/code]

Custom socket path (for example rootless Docker):

Copy
[code]
    export OPENCLAW_SANDBOX=1
    export OPENCLAW_DOCKER_SOCKET=/run/user/1000/docker.sock
    ./docker-setup.sh
    
[/code]

Notes:

  * The script mounts `docker.sock` only after sandbox prerequisites pass.
  * If sandbox setup cannot be completed, the script resets `agents.defaults.sandbox.mode` to `off` to avoid stale/broken sandbox config on reruns.
  * If `Dockerfile.sandbox` is missing, the script prints a warning and continues; build `openclaw-sandbox:bookworm-slim` with `scripts/sandbox-setup.sh` if needed.
  * For non-local `OPENCLAW_IMAGE` values, the image must already contain Docker CLI support for sandbox execution.

### 

​

Automation/CI (non-interactive, no TTY noise)

For scripts and CI, disable Compose pseudo-TTY allocation with `-T`:

Copy
[code]
    docker compose run -T --rm openclaw-cli gateway probe
    docker compose run -T --rm openclaw-cli devices list --json
    
[/code]

If your automation exports no Claude session vars, leaving them unset now resolves to empty values by default in `docker-compose.yml` to avoid repeated “variable is not set” warnings.

### 

​

Shared-network security note (CLI + gateway)

`openclaw-cli` uses `network_mode: "service:openclaw-gateway"` so CLI commands can reliably reach the gateway over `127.0.0.1` in Docker. Treat this as a shared trust boundary: loopback binding is not isolation between these two containers. If you need stronger separation, run commands from a separate container/host network path instead of the bundled `openclaw-cli` service. To reduce impact if the CLI process is compromised, the compose config drops `NET_RAW`/`NET_ADMIN` and enables `no-new-privileges` on `openclaw-cli`. It writes config/workspace on the host:

  * `~/.openclaw/`
  * `~/.openclaw/workspace`

Running on a VPS? See [Hetzner (Docker VPS)](https://docs.openclaw.ai/install/</install/hetzner>).

### 

​

Use a remote image (skip local build)

Official pre-built images are published at:

  * [GitHub Container Registry package](https://docs.openclaw.ai/install/<https:/github.com/openclaw/openclaw/pkgs/container/openclaw>)

Use image name `ghcr.io/openclaw/openclaw` (not similarly named Docker Hub images). Common tags:

  * `main` — latest build from `main`
  * `<version>` — release tag builds (for example `2026.2.26`)
  * `latest` — latest stable release tag

### 

​

Base image metadata

The main Docker image currently uses:

  * `node:24-bookworm`

The docker image now publishes OCI base-image annotations (sha256 is an example, and points at the pinned multi-arch manifest list for that tag):

  * `org.opencontainers.image.base.name=docker.io/library/node:24-bookworm`
  * `org.opencontainers.image.base.digest=sha256:3a09aa6354567619221ef6c45a5051b671f953f0a1924d1f819ffb236e520e6b`
  * `org.opencontainers.image.source=https://github.com/openclaw/openclaw`
  * `org.opencontainers.image.url=https://openclaw.ai`
  * `org.opencontainers.image.documentation=https://docs.openclaw.ai/install/docker`
  * `org.opencontainers.image.licenses=MIT`
  * `org.opencontainers.image.title=OpenClaw`
  * `org.opencontainers.image.description=OpenClaw gateway and CLI runtime container image`
  * `org.opencontainers.image.revision=<git-sha>`
  * `org.opencontainers.image.version=<tag-or-main>`
  * `org.opencontainers.image.created=<rfc3339 timestamp>`

Reference: [OCI image annotations](https://docs.openclaw.ai/install/<https:/github.com/opencontainers/image-spec/blob/main/annotations.md>) Release context: this repository’s tagged history already uses Bookworm in `v2026.2.22` and earlier 2026 tags (for example `v2026.2.21`, `v2026.2.9`). By default the setup script builds the image from source. To pull a pre-built image instead, set `OPENCLAW_IMAGE` before running the script:

Copy
[code]
    export OPENCLAW_IMAGE="ghcr.io/openclaw/openclaw:latest"
    ./docker-setup.sh
    
[/code]

The script detects that `OPENCLAW_IMAGE` is not the default `openclaw:local` and runs `docker pull` instead of `docker build`. Everything else (onboarding, gateway start, token generation) works the same way. `docker-setup.sh` still runs from the repository root because it uses the local `docker-compose.yml` and helper files. `OPENCLAW_IMAGE` skips local image build time; it does not replace the compose/setup workflow.

### 

​

Shell Helpers (optional)

For easier day-to-day Docker management, install `ClawDock`:

Copy
[code]
    mkdir -p ~/.clawdock && curl -sL https://raw.githubusercontent.com/openclaw/openclaw/main/scripts/shell-helpers/clawdock-helpers.sh -o ~/.clawdock/clawdock-helpers.sh
    
[/code]

**Add to your shell config (zsh):**

Copy
[code]
    echo 'source ~/.clawdock/clawdock-helpers.sh' >> ~/.zshrc && source ~/.zshrc
    
[/code]

Then use `clawdock-start`, `clawdock-stop`, `clawdock-dashboard`, etc. Run `clawdock-help` for all commands. See [`ClawDock` Helper README](https://docs.openclaw.ai/install/<https:/github.com/openclaw/openclaw/blob/main/scripts/shell-helpers/README.md>) for details.

### 

​

Manual flow (compose)

Copy
[code]
    docker build -t openclaw:local -f Dockerfile .
    docker compose run --rm openclaw-cli onboard
    docker compose up -d openclaw-gateway
    
[/code]

Note: run `docker compose ...` from the repo root. If you enabled `OPENCLAW_EXTRA_MOUNTS` or `OPENCLAW_HOME_VOLUME`, the setup script writes `docker-compose.extra.yml`; include it when running Compose elsewhere:

Copy
[code]
    docker compose -f docker-compose.yml -f docker-compose.extra.yml <command>
    
[/code]

### 

​

Control UI token + pairing (Docker)

If you see “unauthorized” or “disconnected (1008): pairing required”, fetch a fresh dashboard link and approve the browser device:

Copy
[code]
    docker compose run --rm openclaw-cli dashboard --no-open
    docker compose run --rm openclaw-cli devices list
    docker compose run --rm openclaw-cli devices approve <requestId>
    
[/code]

More detail: [Dashboard](https://docs.openclaw.ai/install/</web/dashboard>), [Devices](https://docs.openclaw.ai/install/</cli/devices>).

### 

​

Extra mounts (optional)

If you want to mount additional host directories into the containers, set `OPENCLAW_EXTRA_MOUNTS` before running `docker-setup.sh`. This accepts a comma-separated list of Docker bind mounts and applies them to both `openclaw-gateway` and `openclaw-cli` by generating `docker-compose.extra.yml`. Example:

Copy
[code]
    export OPENCLAW_EXTRA_MOUNTS="$HOME/.codex:/home/node/.codex:ro,$HOME/github:/home/node/github:rw"
    ./docker-setup.sh
    
[/code]

Notes:

  * Paths must be shared with Docker Desktop on macOS/Windows.
  * Each entry must be `source:target[:options]` with no spaces, tabs, or newlines.
  * If you edit `OPENCLAW_EXTRA_MOUNTS`, rerun `docker-setup.sh` to regenerate the extra compose file.
  * `docker-compose.extra.yml` is generated. Don’t hand-edit it.

### 

​

Persist the entire container home (optional)

If you want `/home/node` to persist across container recreation, set a named volume via `OPENCLAW_HOME_VOLUME`. This creates a Docker volume and mounts it at `/home/node`, while keeping the standard config/workspace bind mounts. Use a named volume here (not a bind path); for bind mounts, use `OPENCLAW_EXTRA_MOUNTS`. Example:

Copy
[code]
    export OPENCLAW_HOME_VOLUME="openclaw_home"
    ./docker-setup.sh
    
[/code]

You can combine this with extra mounts:

Copy
[code]
    export OPENCLAW_HOME_VOLUME="openclaw_home"
    export OPENCLAW_EXTRA_MOUNTS="$HOME/.codex:/home/node/.codex:ro,$HOME/github:/home/node/github:rw"
    ./docker-setup.sh
    
[/code]

Notes:

  * Named volumes must match `^[A-Za-z0-9][A-Za-z0-9_.-]*$`.
  * If you change `OPENCLAW_HOME_VOLUME`, rerun `docker-setup.sh` to regenerate the extra compose file.
  * The named volume persists until removed with `docker volume rm <name>`.

### 

​

Install extra apt packages (optional)

If you need system packages inside the image (for example, build tools or media libraries), set `OPENCLAW_DOCKER_APT_PACKAGES` before running `docker-setup.sh`. This installs the packages during the image build, so they persist even if the container is deleted. Example:

Copy
[code]
    export OPENCLAW_DOCKER_APT_PACKAGES="ffmpeg build-essential"
    ./docker-setup.sh
    
[/code]

Notes:

  * This accepts a space-separated list of apt package names.
  * If you change `OPENCLAW_DOCKER_APT_PACKAGES`, rerun `docker-setup.sh` to rebuild the image.

### 

​

Pre-install extension dependencies (optional)

Extensions with their own `package.json` (e.g. `diagnostics-otel`, `matrix`, `msteams`) install their npm dependencies on first load. To bake those dependencies into the image instead, set `OPENCLAW_EXTENSIONS` before running `docker-setup.sh`:

Copy
[code]
    export OPENCLAW_EXTENSIONS="diagnostics-otel matrix"
    ./docker-setup.sh
    
[/code]

Or when building directly:

Copy
[code]
    docker build --build-arg OPENCLAW_EXTENSIONS="diagnostics-otel matrix" .
    
[/code]

Notes:

  * This accepts a space-separated list of extension directory names (under `extensions/`).
  * Only extensions with a `package.json` are affected; lightweight plugins without one are ignored.
  * If you change `OPENCLAW_EXTENSIONS`, rerun `docker-setup.sh` to rebuild the image.

### 

​

Power-user / full-featured container (opt-in)

The default Docker image is **security-first** and runs as the non-root `node` user. This keeps the attack surface small, but it means:

  * no system package installs at runtime
  * no Homebrew by default
  * no bundled Chromium/Playwright browsers

If you want a more full-featured container, use these opt-in knobs:

  1. **Persist`/home/node`** so browser downloads and tool caches survive:

Copy
[code]
    export OPENCLAW_HOME_VOLUME="openclaw_home"
    ./docker-setup.sh
    
[/code]

  2. **Bake system deps into the image** (repeatable + persistent):

Copy
[code]
    export OPENCLAW_DOCKER_APT_PACKAGES="git curl jq"
    ./docker-setup.sh
    
[/code]

  3. **Install Playwright browsers without`npx`** (avoids npm override conflicts):

Copy
[code]
    docker compose run --rm openclaw-cli \
      node /app/node_modules/playwright-core/cli.js install chromium
    
[/code]

If you need Playwright to install system deps, rebuild the image with `OPENCLAW_DOCKER_APT_PACKAGES` instead of using `--with-deps` at runtime.

  4. **Persist Playwright browser downloads** :

  * Set `PLAYWRIGHT_BROWSERS_PATH=/home/node/.cache/ms-playwright` in `docker-compose.yml`.
  * Ensure `/home/node` persists via `OPENCLAW_HOME_VOLUME`, or mount `/home/node/.cache/ms-playwright` via `OPENCLAW_EXTRA_MOUNTS`.

### 

​

Permissions + EACCES

The image runs as `node` (uid 1000). If you see permission errors on `/home/node/.openclaw`, make sure your host bind mounts are owned by uid 1000. Example (Linux host):

Copy
[code]
    sudo chown -R 1000:1000 /path/to/openclaw-config /path/to/openclaw-workspace
    
[/code]

If you choose to run as root for convenience, you accept the security tradeoff.

### 

​

Faster rebuilds (recommended)

To speed up rebuilds, order your Dockerfile so dependency layers are cached. This avoids re-running `pnpm install` unless lockfiles change:

Copy
[code]
    FROM node:24-bookworm
    
    # Install Bun (required for build scripts)
    RUN curl -fsSL https://bun.sh/install | bash
    ENV PATH="/root/.bun/bin:${PATH}"
    
    RUN corepack enable
    
    WORKDIR /app
    
    # Cache dependencies unless package metadata changes
    COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./
    COPY ui/package.json ./ui/package.json
    COPY scripts ./scripts
    
    RUN pnpm install --frozen-lockfile
    
    COPY . .
    RUN pnpm build
    RUN pnpm ui:install
    RUN pnpm ui:build
    
    ENV NODE_ENV=production
    
    CMD ["node","dist/index.js"]
    
[/code]

### 

​

Channel setup (optional)

Use the CLI container to configure channels, then restart the gateway if needed. WhatsApp (QR):

Copy
[code]
    docker compose run --rm openclaw-cli channels login
    
[/code]

Telegram (bot token):

Copy
[code]
    docker compose run --rm openclaw-cli channels add --channel telegram --token "<token>"
    
[/code]

Discord (bot token):

Copy
[code]
    docker compose run --rm openclaw-cli channels add --channel discord --token "<token>"
    
[/code]

Docs: [WhatsApp](https://docs.openclaw.ai/install/</channels/whatsapp>), [Telegram](https://docs.openclaw.ai/install/</channels/telegram>), [Discord](https://docs.openclaw.ai/install/</channels/discord>)

### 

​

OpenAI Codex OAuth (headless Docker)

If you pick OpenAI Codex OAuth in the wizard, it opens a browser URL and tries to capture a callback on `http://127.0.0.1:1455/auth/callback`. In Docker or headless setups that callback can show a browser error. Copy the full redirect URL you land on and paste it back into the wizard to finish auth.

### 

​

Health checks

Container probe endpoints (no auth required):

Copy
[code]
    curl -fsS http://127.0.0.1:18789/healthz
    curl -fsS http://127.0.0.1:18789/readyz
    
[/code]

Aliases: `/health` and `/ready`. `/healthz` is a shallow liveness probe for “the gateway process is up”. `/readyz` stays ready during startup grace, then becomes `503` only if required managed channels are still disconnected after grace or disconnect later. The Docker image includes a built-in `HEALTHCHECK` that pings `/healthz` in the background. In plain terms: Docker keeps checking if OpenClaw is still responsive. If checks keep failing, Docker marks the container as `unhealthy`, and orchestration systems (Docker Compose restart policy, Swarm, Kubernetes, etc.) can automatically restart or replace it. Authenticated deep health snapshot (gateway + channels):

Copy
[code]
    docker compose exec openclaw-gateway node dist/index.js health --token "$OPENCLAW_GATEWAY_TOKEN"
    
[/code]

### 

​

E2E smoke test (Docker)

Copy
[code]
    scripts/e2e/onboard-docker.sh
    
[/code]

### 

​

QR import smoke test (Docker)

Copy
[code]
    pnpm test:docker:qr
    
[/code]

### 

​

LAN vs loopback (Docker Compose)

`docker-setup.sh` defaults `OPENCLAW_GATEWAY_BIND=lan` so host access to `http://127.0.0.1:18789` works with Docker port publishing.

  * `lan` (default): host browser + host CLI can reach the published gateway port.
  * `loopback`: only processes inside the container network namespace can reach the gateway directly; host-published port access may fail.

The setup script also pins `gateway.mode=local` after onboarding so Docker CLI commands default to local loopback targeting. Legacy config note: use bind mode values in `gateway.bind` (`lan` / `loopback` / `custom` / `tailnet` / `auto`), not host aliases (`0.0.0.0`, `127.0.0.1`, `localhost`, `::`, `::1`). If you see `Gateway target: ws://172.x.x.x:18789` or repeated `pairing required` errors from Docker CLI commands, run:

Copy
[code]
    docker compose run --rm openclaw-cli config set gateway.mode local
    docker compose run --rm openclaw-cli config set gateway.bind lan
    docker compose run --rm openclaw-cli devices list --url ws://127.0.0.1:18789
    
[/code]

### 

​

Notes

  * Gateway bind defaults to `lan` for container use (`OPENCLAW_GATEWAY_BIND`).
  * Dockerfile CMD uses `--allow-unconfigured`; mounted config with `gateway.mode` not `local` will still start. Override CMD to enforce the guard.
  * The gateway container is the source of truth for sessions (`~/.openclaw/agents/<agentId>/sessions/`).

### 

​

Storage model

  * **Persistent host data:** Docker Compose bind-mounts `OPENCLAW_CONFIG_DIR` to `/home/node/.openclaw` and `OPENCLAW_WORKSPACE_DIR` to `/home/node/.openclaw/workspace`, so those paths survive container replacement.
  * **Ephemeral sandbox tmpfs:** when `agents.defaults.sandbox` is enabled, the sandbox containers use `tmpfs` for `/tmp`, `/var/tmp`, and `/run`. Those mounts are separate from the top-level Compose stack and disappear with the sandbox container.
  * **Disk growth hotspots:** watch `media/`, `agents/<agentId>/sessions/sessions.json`, transcript JSONL files, `cron/runs/*.jsonl`, and rolling file logs under `/tmp/openclaw/` (or your configured `logging.file`). If you also run the macOS app outside Docker, its service logs are separate again: `~/.openclaw/logs/gateway.log`, `~/.openclaw/logs/gateway.err.log`, and `/tmp/openclaw/openclaw-gateway.log`.

## 

​

Agent Sandbox (host gateway + Docker tools)

Deep dive: [Sandboxing](https://docs.openclaw.ai/install/</gateway/sandboxing>)

### 

​

What it does

When `agents.defaults.sandbox` is enabled, **non-main sessions** run tools inside a Docker container. The gateway stays on your host, but the tool execution is isolated:

  * scope: `"agent"` by default (one container + workspace per agent)
  * scope: `"session"` for per-session isolation
  * per-scope workspace folder mounted at `/workspace`
  * optional agent workspace access (`agents.defaults.sandbox.workspaceAccess`)
  * allow/deny tool policy (deny wins)
  * inbound media is copied into the active sandbox workspace (`media/inbound/*`) so tools can read it (with `workspaceAccess: "rw"`, this lands in the agent workspace)

Warning: `scope: "shared"` disables cross-session isolation. All sessions share one container and one workspace.

### 

​

Per-agent sandbox profiles (multi-agent)

If you use multi-agent routing, each agent can override sandbox + tool settings: `agents.list[].sandbox` and `agents.list[].tools` (plus `agents.list[].tools.sandbox.tools`). This lets you run mixed access levels in one gateway:

  * Full access (personal agent)
  * Read-only tools + read-only workspace (family/work agent)
  * No filesystem/shell tools (public agent)

See [Multi-Agent Sandbox & Tools](https://docs.openclaw.ai/install/</tools/multi-agent-sandbox-tools>) for examples, precedence, and troubleshooting.

### 

​

Default behavior

  * Image: `openclaw-sandbox:bookworm-slim`
  * One container per agent
  * Agent workspace access: `workspaceAccess: "none"` (default) uses `~/.openclaw/sandboxes`
    * `"ro"` keeps the sandbox workspace at `/workspace` and mounts the agent workspace read-only at `/agent` (disables `write`/`edit`/`apply_patch`)
    * `"rw"` mounts the agent workspace read/write at `/workspace`
  * Auto-prune: idle > 24h OR age > 7d
  * Network: `none` by default (explicitly opt-in if you need egress)
    * `host` is blocked.
    * `container:<id>` is blocked by default (namespace-join risk).
  * Default allow: `exec`, `process`, `read`, `write`, `edit`, `sessions_list`, `sessions_history`, `sessions_send`, `sessions_spawn`, `session_status`
  * Default deny: `browser`, `canvas`, `nodes`, `cron`, `discord`, `gateway`

### 

​

Enable sandboxing

If you plan to install packages in `setupCommand`, note:

  * Default `docker.network` is `"none"` (no egress).
  * `docker.network: "host"` is blocked.
  * `docker.network: "container:<id>"` is blocked by default.
  * Break-glass override: `agents.defaults.sandbox.docker.dangerouslyAllowContainerNamespaceJoin: true`.
  * `readOnlyRoot: true` blocks package installs.
  * `user` must be root for `apt-get` (omit `user` or set `user: "0:0"`). OpenClaw auto-recreates containers when `setupCommand` (or docker config) changes unless the container was **recently used** (within ~5 minutes). Hot containers log a warning with the exact `openclaw sandbox recreate ...` command.

Copy
[code]
    {
      agents: {
        defaults: {
          sandbox: {
            mode: "non-main", // off | non-main | all
            scope: "agent", // session | agent | shared (agent is default)
            workspaceAccess: "none", // none | ro | rw
            workspaceRoot: "~/.openclaw/sandboxes",
            docker: {
              image: "openclaw-sandbox:bookworm-slim",
              workdir: "/workspace",
              readOnlyRoot: true,
              tmpfs: ["/tmp", "/var/tmp", "/run"],
              network: "none",
              user: "1000:1000",
              capDrop: ["ALL"],
              env: { LANG: "C.UTF-8" },
              setupCommand: "apt-get update && apt-get install -y git curl jq",
              pidsLimit: 256,
              memory: "1g",
              memorySwap: "2g",
              cpus: 1,
              ulimits: {
                nofile: { soft: 1024, hard: 2048 },
                nproc: 256,
              },
              seccompProfile: "/path/to/seccomp.json",
              apparmorProfile: "openclaw-sandbox",
              dns: ["1.1.1.1", "8.8.8.8"],
              extraHosts: ["internal.service:10.0.0.5"],
            },
            prune: {
              idleHours: 24, // 0 disables idle pruning
              maxAgeDays: 7, // 0 disables max-age pruning
            },
          },
        },
      },
      tools: {
        sandbox: {
          tools: {
            allow: [
              "exec",
              "process",
              "read",
              "write",
              "edit",
              "sessions_list",
              "sessions_history",
              "sessions_send",
              "sessions_spawn",
              "session_status",
            ],
            deny: ["browser", "canvas", "nodes", "cron", "discord", "gateway"],
          },
        },
      },
    }
    
[/code]

Hardening knobs live under `agents.defaults.sandbox.docker`: `network`, `user`, `pidsLimit`, `memory`, `memorySwap`, `cpus`, `ulimits`, `seccompProfile`, `apparmorProfile`, `dns`, `extraHosts`, `dangerouslyAllowContainerNamespaceJoin` (break-glass only). Multi-agent: override `agents.defaults.sandbox.{docker,browser,prune}.*` per agent via `agents.list[].sandbox.{docker,browser,prune}.*` (ignored when `agents.defaults.sandbox.scope` / `agents.list[].sandbox.scope` is `"shared"`).

### 

​

Build the default sandbox image

Copy
[code]
    scripts/sandbox-setup.sh
    
[/code]

This builds `openclaw-sandbox:bookworm-slim` using `Dockerfile.sandbox`.

### 

​

Sandbox common image (optional)

If you want a sandbox image with common build tooling (Node, Go, Rust, etc.), build the common image:

Copy
[code]
    scripts/sandbox-common-setup.sh
    
[/code]

This builds `openclaw-sandbox-common:bookworm-slim`. To use it:

Copy
[code]
    {
      agents: {
        defaults: {
          sandbox: { docker: { image: "openclaw-sandbox-common:bookworm-slim" } },
        },
      },
    }
    
[/code]

### 

​

Sandbox browser image

To run the browser tool inside the sandbox, build the browser image:

Copy
[code]
    scripts/sandbox-browser-setup.sh
    
[/code]

This builds `openclaw-sandbox-browser:bookworm-slim` using `Dockerfile.sandbox-browser`. The container runs Chromium with CDP enabled and an optional noVNC observer (headful via Xvfb). Notes:

  * Docker and other headless/container browser flows stay on raw CDP. Chrome MCP `existing-session` is for host-local Chrome, not container takeover.
  * Headful (Xvfb) reduces bot blocking vs headless.
  * Headless can still be used by setting `agents.defaults.sandbox.browser.headless=true`.
  * No full desktop environment (GNOME) is needed; Xvfb provides the display.
  * Browser containers default to a dedicated Docker network (`openclaw-sandbox-browser`) instead of global `bridge`.
  * Optional `agents.defaults.sandbox.browser.cdpSourceRange` restricts container-edge CDP ingress by CIDR (for example `172.21.0.1/32`).
  * noVNC observer access is password-protected by default; OpenClaw provides a short-lived observer token URL that serves a local bootstrap page and keeps the password in URL fragment (instead of URL query).
  * Browser container startup defaults are conservative for shared/container workloads, including:
    * `--remote-debugging-address=127.0.0.1`
    * `--remote-debugging-port=<derived from OPENCLAW_BROWSER_CDP_PORT>`
    * `--user-data-dir=${HOME}/.chrome`
    * `--no-first-run`
    * `--no-default-browser-check`
    * `--disable-3d-apis`
    * `--disable-software-rasterizer`
    * `--disable-gpu`
    * `--disable-dev-shm-usage`
    * `--disable-background-networking`
    * `--disable-features=TranslateUI`
    * `--disable-breakpad`
    * `--disable-crash-reporter`
    * `--metrics-recording-only`
    * `--renderer-process-limit=2`
    * `--no-zygote`
    * `--disable-extensions`
    * If `agents.defaults.sandbox.browser.noSandbox` is set, `--no-sandbox` and `--disable-setuid-sandbox` are also appended.
    * The three graphics hardening flags above are optional. If your workload needs WebGL/3D, set `OPENCLAW_BROWSER_DISABLE_GRAPHICS_FLAGS=0` to run without `--disable-3d-apis`, `--disable-software-rasterizer`, and `--disable-gpu`.
    * Extension behavior is controlled by `--disable-extensions` and can be disabled (enables extensions) via `OPENCLAW_BROWSER_DISABLE_EXTENSIONS=0` for extension-dependent pages or extensions-heavy workflows.
    * `--renderer-process-limit=2` is also configurable with `OPENCLAW_BROWSER_RENDERER_PROCESS_LIMIT`; set `0` to let Chromium choose its default process limit when browser concurrency needs tuning.

Defaults are applied by default in the bundled image. If you need different Chromium flags, use a custom browser image and provide your own entrypoint. Use config:

Copy
[code]
    {
      agents: {
        defaults: {
          sandbox: {
            browser: { enabled: true },
          },
        },
      },
    }
    
[/code]

Custom browser image:

Copy
[code]
    {
      agents: {
        defaults: {
          sandbox: { browser: { image: "my-openclaw-browser" } },
        },
      },
    }
    
[/code]

When enabled, the agent receives:

  * a sandbox browser control URL (for the `browser` tool)
  * a noVNC URL (if enabled and headless=false)

Remember: if you use an allowlist for tools, add `browser` (and remove it from deny) or the tool remains blocked. Prune rules (`agents.defaults.sandbox.prune`) apply to browser containers too.

### 

​

Custom sandbox image

Build your own image and point config to it:

Copy
[code]
    docker build -t my-openclaw-sbx -f Dockerfile.sandbox .
    
[/code]

Copy
[code]
    {
      agents: {
        defaults: {
          sandbox: { docker: { image: "my-openclaw-sbx" } },
        },
      },
    }
    
[/code]

### 

​

Tool policy (allow/deny)

  * `deny` wins over `allow`.
  * If `allow` is empty: all tools (except deny) are available.
  * If `allow` is non-empty: only tools in `allow` are available (minus deny).

### 

​

Pruning strategy

Two knobs:

  * `prune.idleHours`: remove containers not used in X hours (0 = disable)
  * `prune.maxAgeDays`: remove containers older than X days (0 = disable)

Example:

  * Keep busy sessions but cap lifetime: `idleHours: 24`, `maxAgeDays: 7`
  * Never prune: `idleHours: 0`, `maxAgeDays: 0`

### 

​

Security notes

  * Hard wall only applies to **tools** (exec/read/write/edit/apply_patch).
  * Host-only tools like browser/camera/canvas are blocked by default.
  * Allowing `browser` in sandbox **breaks isolation** (browser runs on host).

## 

​

Troubleshooting

  * Image missing: build with [`scripts/sandbox-setup.sh`](https://docs.openclaw.ai/install/<https:/github.com/openclaw/openclaw/blob/main/scripts/sandbox-setup.sh>) or set `agents.defaults.sandbox.docker.image`.
  * Container not running: it will auto-create per session on demand.
  * Permission errors in sandbox: set `docker.user` to a UID:GID that matches your mounted workspace ownership (or chown the workspace folder).
  * Custom tools not found: OpenClaw runs commands with `sh -lc` (login shell), which sources `/etc/profile` and may reset PATH. Set `docker.env.PATH` to prepend your custom tool paths (e.g., `/custom/bin:/usr/local/share/npm-global/bin`), or add a script under `/etc/profile.d/` in your Dockerfile.

[Installer Internals](https://docs.openclaw.ai/install/</install/installer>)[Podman](https://docs.openclaw.ai/install/</install/podman>)

⌘I

[Powered byThis documentation is built and hosted on Mintlify, a developer documentation platform](https://docs.openclaw.ai/install/<https:/www.mintlify.com?utm_campaign=poweredBy&utm_medium=referral&utm_source=clawdhub>)