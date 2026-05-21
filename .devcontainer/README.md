# devcontainer-config

Reusable dev container config for projects that run on the `devcontainer-runner` VM.

## Quick start

In a new or existing project:

```bash
git clone https://<TOKEN>@github.com/cogx-sol/devcontainer-config.git .devcontainer
rm -rf .devcontainer/.git
git add .devcontainer && git commit -m "add devcontainer"
```

The `--mode=git` flag makes degit clone via git instead of downloading a tarball — required for private repos (works for public ones too).

Then open the project in VS Code, connect to the `devcontainer-runner` VM via Remote-SSH, and **Reopen in Container**.

## Requirements

- `devcontainer-runner-base:latest` image built on the host. See the [devcontainer-runner project](https://github.com/cogx-sol/homelab/tree/main/devcontainer-runner) for base-image build instructions.
- VS Code with **Remote - SSH** and **Dev Containers** extensions.

## Updating an existing project

```bash
npx degit --mode=git --force cogx-sol/devcontainer-config .devcontainer
```

Review the diff before committing.

## Adding project-specific dependencies

Two paths:

1. **`postCreateCommand`** — quick installs after the container starts (`pip install`, `pnpm install`, `apt install` via `sudo`). Already wired in `devcontainer.json`.

2. **Project Dockerfile** — for things you want baked into the image so they survive rebuilds:

   In the project's `.devcontainer/`, create a `Dockerfile`:
   ```dockerfile
   FROM devcontainer-runner-base:latest
   RUN sudo apt-get update && sudo apt-get install -y <your-deps>
   ```

   In `devcontainer.json`, replace the `image` line with:
   ```jsonc
   "build": { "dockerfile": "Dockerfile" }
   ```
