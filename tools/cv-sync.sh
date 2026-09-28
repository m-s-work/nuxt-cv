#!/usr/bin/env bash
# CV as Code: deploy a tenant folder from a Git repository to the CV API.
#
#   tools/cv-sync.sh [--check|--verify] <tenant-dir> [tenant-id]
#
# <tenant-dir> contains tenant.json, cv.<locale>.json and optionally assets/.
# tenant-id defaults to the folder name.
#
#   --check   validate only (JSON syntax, required files, referenced assets); nothing is uploaded
#   --verify  compare the SHA-256 of every local file with the server (GET /admin/tenants/<id>/hash);
#             exit 1 if anything differs – e.g. to check that a deployment is up to date
#
# Environment (not needed for --check):
#   CV_API_URL         e.g. https://cv.velarix.space/api
#   CV_ADMIN_API_KEY   admin key of the API
#   CV_REVISION        git SHA to register the deployed CV as (default: HEAD of the repo containing
#                      <tenant-dir>). Invites can be pinned to registered revisions; the admin UI warns
#                      when a pinned revision is outdated. Set to "-" to skip registration.
#   CV_GIT_REPO        HTTPS URL of the CV repository (default: remote "origin", ssh form converted to https).
#                      The API fetches pruned revisions from there again when an invite is pinned to them
#                      (private repos: set Git__Token / CV_GIT_TOKEN on the API).
#
# Exit code != 0 on any validation or upload error, so CI pipelines fail visibly.
set -euo pipefail

check_only=false
verify_only=false
if [[ "${1:-}" == "--check" ]]; then check_only=true; shift; fi
if [[ "${1:-}" == "--verify" ]]; then verify_only=true; shift; fi

dir="${1:?usage: cv-sync.sh [--check] <tenant-dir> [tenant-id]}"
dir="${dir%/}"
tenant="${2:-$(basename "$dir")}"

fail() { echo "error: $*" >&2; exit 1; }

[[ "$tenant" =~ ^[a-z0-9][a-z0-9-]{0,62}$ ]] || fail "invalid tenant id '$tenant' (a-z, 0-9, -)"
[[ -f "$dir/tenant.json" ]] || fail "$dir/tenant.json missing"
compgen -G "$dir/cv.*.json" >/dev/null || fail "no cv.<locale>.json in $dir"

# JSON files may contain // comments (the API accepts them); strip them for validation.
validate_json() {
  node -e '
    const fs = require("fs");
    const text = fs.readFileSync(process.argv[1], "utf8")
      .replace(/("(?:[^"\\]|\\.)*")|\/\/[^\n]*|\/\*[\s\S]*?\*\//g, (m, str) => str ?? "")
      .replace(/,(\s*[}\]])/g, "$1");
    JSON.parse(text);
  ' "$1" 2>/dev/null || fail "$1 is not valid JSON"
}

files=("tenant.json")
for f in "$dir"/cv.*.json; do
  name="$(basename "$f")"
  [[ "$name" =~ ^cv\.[a-z]{2}(-[A-Z]{2})?\.json$ ]] || fail "unexpected file name $name"
  files+=("$name")
done
for f in "${files[@]}"; do validate_json "$dir/$f"; done

# Every /api/assets/<file> referenced in a CV must exist in assets/.
missing=0
while read -r asset; do
  [[ -z "$asset" ]] && continue
  if [[ ! -f "$dir/assets/$asset" ]]; then echo "error: referenced asset assets/$asset missing" >&2; missing=1; fi
done < <(grep -ohE '/api/assets/[A-Za-z0-9._-]+' "$dir"/cv.*.json | sed 's#/api/assets/##' | sort -u)
(( missing == 0 )) || exit 1

if [[ -d "$dir/assets" ]]; then
  while IFS= read -r -d '' f; do
    name="$(basename "$f")"
    [[ "$name" =~ ^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$ ]] || fail "invalid asset name $name"
    files+=("assets/$name")
  done < <(find "$dir/assets" -maxdepth 1 -type f -print0 | sort -z)
fi

echo "tenant '$tenant': ${#files[@]} file(s) valid"
$check_only && exit 0

: "${CV_API_URL:?CV_API_URL not set}"
: "${CV_ADMIN_API_KEY:?CV_ADMIN_API_KEY not set}"

if $verify_only; then
  remote=$(curl -sS -f -H "X-Admin-Key: $CV_ADMIN_API_KEY" "${CV_API_URL%/}/admin/tenants/$tenant/hash") \
    || fail "could not read hashes of tenant '$tenant' from the server"
  local_list=$(for f in "${files[@]}"; do printf '%s %s\n' "$f" "$(sha256sum "$dir/$f" | cut -d' ' -f1)"; done)
  LOCAL="$local_list" REMOTE="$remote" node -e '
    const remote = JSON.parse(process.env.REMOTE).files
    const local = Object.fromEntries(process.env.LOCAL.trim().split("\n").map(l => l.split(" ")))
    let diff = 0
    for (const [path, hash] of Object.entries(local)) {
      if (!remote[path]) { console.log(`missing on server: ${path}`); diff++ }
      else if (remote[path] !== hash) { console.log(`differs:           ${path}`); diff++ }
    }
    for (const path of Object.keys(remote)) if (!local[path]) console.log(`only on server:    ${path}`)
    process.exit(diff ? 1 : 0)
  ' || fail "server data of tenant '$tenant' is not up to date"
  echo "tenant '$tenant': server matches local files"
  exit 0
fi

# Assets first, tenant.json last: the CV never references an asset that is not uploaded yet.
ordered=()
for f in "${files[@]}"; do [[ "$f" == assets/* ]] && ordered+=("$f"); done
for f in "${files[@]}"; do [[ "$f" == cv.* ]] && ordered+=("$f"); done
ordered+=("tenant.json")

for f in "${ordered[@]}"; do
  status=$(curl -sS -o /tmp/cv-sync-response -w '%{http_code}' -X PUT \
    -H "X-Admin-Key: $CV_ADMIN_API_KEY" \
    --data-binary "@$dir/$f" \
    "${CV_API_URL%/}/admin/tenants/$tenant/files/$f")
  [[ "$status" == 204 ]] || fail "upload of $f failed ($status): $(cat /tmp/cv-sync-response)"
  echo "uploaded $f"
done

# Register the deployed CV as a revision (snapshot) under its git commit.
revision="${CV_REVISION:-$(git -C "$dir" rev-parse HEAD 2>/dev/null || true)}"
if [[ -z "$revision" || "$revision" == "-" ]]; then
  echo "note: no git revision (not a git checkout and CV_REVISION unset); CV not registered as revision"
else
  if [[ -n "$(git -C "$dir" status --porcelain -- . 2>/dev/null)" ]]; then
    echo "warning: $dir has uncommitted changes; registering them as $revision anyway" >&2
  fi
  message="$(git -C "$dir" log -1 --format=%s "$revision" 2>/dev/null || true)"
  committed="$(git -C "$dir" log -1 --format=%cI "$revision" 2>/dev/null || true)"
  repo="${CV_GIT_REPO:-$(git -C "$dir" remote get-url origin 2>/dev/null || true)}"
  # git@host:owner/repo.git -> https://host/owner/repo.git; drop credentials embedded in the URL.
  if [[ "$repo" =~ ^[^@/]+@([^:]+):(.+)$ ]]; then repo="https://${BASH_REMATCH[1]}/${BASH_REMATCH[2]}"; fi
  repo="$(sed -E 's#^(https?://)[^@/]+@#\1#' <<<"$repo")"
  repo_path="$(git -C "$dir" rev-parse --show-prefix 2>/dev/null || true)"
  body=$(REV="$revision" MSG="$message" AT="$committed" REPO="$repo" RPATH="${repo_path%/}" node -e '
    const b = { sha: process.env.REV };
    if (process.env.MSG) b.message = process.env.MSG;
    if (process.env.AT) b.committedAt = process.env.AT;
    if (process.env.REPO.startsWith("https://")) { b.repo = process.env.REPO; b.path = process.env.RPATH; }
    process.stdout.write(JSON.stringify(b));')
  status=$(curl -sS -o /tmp/cv-sync-response -w '%{http_code}' -X POST \
    -H "X-Admin-Key: $CV_ADMIN_API_KEY" -H "Content-Type: application/json" \
    --data "$body" "${CV_API_URL%/}/admin/tenants/$tenant/revisions")
  [[ "$status" == 200 ]] || fail "registering revision $revision failed ($status): $(cat /tmp/cv-sync-response)"
  echo "registered revision ${revision:0:12}"
fi
echo "tenant '$tenant' deployed"
