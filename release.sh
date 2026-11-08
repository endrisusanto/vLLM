#!/usr/bin/env bash
# ==============================================================================
# release.sh - Release automation for endrisusanto/vLLM
# ==============================================================================
set -euo pipefail

REPO="endrisusanto/vLLM"
DEFAULT_VERSION="v1.0.0"
VERSION="${1:-$DEFAULT_VERSION}"
REMOTE_URL="https://github.com/${REPO}.git"

echo "=== Release Script: ${REPO} ==="
echo "Target Version: ${VERSION}"
echo ""

# 1. Check Git
if ! command -v git &>/dev/null; then
  echo "[ERROR] git command not found."
  exit 1
fi

# 2. Initialize repository if needed
if [ ! -d ".git" ]; then
  echo "[1/6] Initializing git repository..."
  git init
  git branch -M main
fi

# 3. Configure Remote
CURRENT_REMOTE=$(git remote get-url origin 2>/dev/null || echo "")
if [ -z "$CURRENT_REMOTE" ]; then
  echo "[2/6] Adding remote origin: ${REMOTE_URL}"
  git remote add origin "${REMOTE_URL}"
else
  echo "[2/6] Remote origin already set to: ${CURRENT_REMOTE}"
fi

# 4. Compile vLLM-Tray.exe if missing
echo "[3/6] Checking vLLM-Tray.exe binary..."
if [ ! -f "vLLM-Tray.exe" ]; then
  if command -v cmd.exe &>/dev/null; then
    echo "  -> Compiling via build-tray.bat..."
    cmd.exe /c build-tray.bat
  elif [ -f "/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe" ]; then
    echo "  -> Compiling via native csc.exe..."
    /c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /optimize+ /win32icon:icon.ico /out:vLLM-Tray.exe /reference:System.Windows.Forms.dll,System.Drawing.dll,System.dll src/TrayApp.cs
  fi
fi

if [ -f "vLLM-Tray.exe" ]; then
  BIN_SIZE=$(wc -c < "vLLM-Tray.exe" | tr -d ' ')
  echo "  [OK] vLLM-Tray.exe ready (${BIN_SIZE} bytes)"
else
  echo "  [WARN] vLLM-Tray.exe not found locally; GitHub Actions will build it on tag push."
fi

# 5. Create local archive
ZIP_NAME="vllm-turnkey-windows-x64-${VERSION}.zip"
echo "[4/6] Creating release archive: ${ZIP_NAME}..."
if command -v zip &>/dev/null; then
  zip -r "${ZIP_NAME}" \
    vLLM-Tray.exe \
    icon.ico \
    app.png \
    build-tray.bat \
    setup.bat \
    start.bat \
    stop.bat \
    docker-compose.yml \
    .env.example \
    README.md \
    models/README.md \
    scripts \
    src \
    -x "*.git*" "models/*/*"
elif command -v powershell.exe &>/dev/null; then
  powershell.exe -NoProfile -Command \
    "Compress-Archive -Path 'vLLM-Tray.exe','icon.ico','app.png','build-tray.bat','setup.bat','start.bat','stop.bat','docker-compose.yml','.env.example','README.md','scripts','src','models' -DestinationPath '${ZIP_NAME}' -Force"
fi

# 6. Commit & Tag
echo "[5/6] Staging files and creating git tag: ${VERSION}..."
if [ -z "$(git config user.name 2>/dev/null || echo '')" ]; then
  git config user.name "Endri Susanto"
fi
if [ -z "$(git config user.email 2>/dev/null || echo '')" ]; then
  git config user.email "endrisusantosekarang@gmail.com"
fi

git add -A
git commit -m "chore(release): ${VERSION} - turnkey vllm package" || true

if git rev-parse "${VERSION}" >/dev/null 2>&1; then
  echo "  Tag ${VERSION} already exists locally, updating tag..."
  git tag -d "${VERSION}"
fi
git tag -a "${VERSION}" -m "Release ${VERSION}"

# 7. Push to GitHub & Create Release
echo "[6/6] Pushing to GitHub (${REPO})..."
echo "Command to push manually if credentials required:"
echo "  git push -u origin main --tags"
echo ""

if git push -u origin main --tags; then
  echo "  [OK] Push completed."

  # Check GitHub CLI
  if command -v gh &>/dev/null; then
    echo "Creating GitHub Release via gh CLI..."
    gh release create "${VERSION}" "${ZIP_NAME}" "vLLM-Tray.exe" \
      --repo "${REPO}" \
      --title "vLLM Turnkey Windows ${VERSION}" \
      --notes "Turnkey Private LLM API for Windows WSL2/Docker with NVIDIA 16GB VRAM, WSL2 Mirrored Networking (ICS Bypass), and Native System Tray Launcher." \
      --draft=false \
      --prerelease=false || echo "Note: gh release create skipped or already exists."
  else
    echo "GitHub Actions will automatically build and publish the release from the pushed tag."
  fi
else
  echo "[WARN] git push failed. Please verify repository access permissions for ${REPO}."
fi

echo ""
echo "=== Release Process Finished: ${VERSION} ==="
