#!/usr/bin/env bash
# CV as Code: deploy a tenant folder from a Git repository to the CV API.
#
#   tools/cv-sync.sh [--check] <tenant-dir> [tenant-id]
#
# <tenant-dir> contains tenant.json, cv.<locale>.json and optionally assets/.
# tenant-id defaults to the folder name.
#
#   --check   validate only (JSON syntax, required files, referenced assets); nothing is uploaded
#
# Environment (not needed for --check):
#   CV_API_URL         e.g. https://cv.velarix.space/api
#   CV_ADMIN_API_KEY   admin key of the API
#
# Exit code != 0 on any validation or upload error, so CI pipelines fail visibly.
set -euo pipefail

check_only=false
if [[ "${1:-}" == "--check" ]]; then check_only=true; shift; fi

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
echo "tenant '$tenant' deployed"
