#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

assembly="Alarmmelder/Properties/AssemblyInfo.cs"
release_dir="Alarmmelder/bin/Release"

get_version_from_file() {
  local file="$1"
  local value=""
  value=$(grep -oE 'AssemblyInformationalVersion\("[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+_Beta"\)' "$file" | head -n1 | sed -E 's/.*\("([0-9.]+)_Beta"\)/\1/' || true)
  if [[ -z "$value" ]]; then
    value=$(grep -oE 'AssemblyVersion\("[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"\)' "$file" | head -n1 | sed -E 's/.*\("([0-9.]+)"\)/\1/' || true)
  fi
  if [[ -z "$value" ]]; then
    echo "Konnte keine Versionsnummer aus $file lesen." >&2
    exit 1
  fi
  printf '%s' "$value"
}

version_gt() {
  local left="$1"
  local right="$2"
  [[ "$left" != "$right" && "$(printf '%s\n%s\n' "$left" "$right" | sort -V | tail -n1)" == "$left" ]]
}

increment_version() {
  local version="$1"
  local major minor patch build
  IFS=. read -r major minor patch build <<< "$version"
  printf '%s.%s.%s.%s' "$major" "$minor" "$patch" "$((build + 1))"
}

current_version=$(get_version_from_file "$assembly")
remote_latest=""

if command -v gh >/dev/null 2>&1 && gh release view beta >/dev/null 2>&1; then
  remote_latest=$(gh release view beta --json assets --jq '.assets[].name' \
    | sed -nE 's/^MioneAlarmmelder-([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)_Beta\.zip$/\1/p' \
    | sort -V \
    | tail -n1 || true)
fi

if [[ -n "$remote_latest" ]] && version_gt "$current_version" "$remote_latest"; then
  release_version="$current_version"
else
  base_version="${remote_latest:-$current_version}"
  release_version=$(increment_version "$base_version")
fi

perl -0pi -e 's/\[assembly: AssemblyVersion\("[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"\)\]/[assembly: AssemblyVersion("'"$release_version"'")]/g; s/\[assembly: AssemblyFileVersion\("[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+"\)\]/[assembly: AssemblyFileVersion("'"$release_version"'")]/g; s/\[assembly: AssemblyInformationalVersion\("[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+_Beta"\)\]/[assembly: AssemblyInformationalVersion("'"$release_version"'_Beta")]/g' "$assembly"

echo "Beta-Version: $current_version -> $release_version"

./build-release-mac.sh

asset_name="MioneAlarmmelder-${release_version}_Beta.zip"
asset_path="$release_dir/$asset_name"
output_zip="$(pwd)/$asset_path"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

rm -f "$asset_path"
mkdir -p "$stage/Assets"
cp "$release_dir/MioneAlarmmelder.exe" "$stage/"
cp "$release_dir/MioneAlarmmelder.exe.config" "$stage/"
cp "$release_dir/MioneAlarmmelder.pdb" "$stage/"
cp -R "$release_dir/Assets/." "$stage/Assets/"
(cd "$stage" && zip -qr "$output_zip" .)

if ! gh release view beta >/dev/null 2>&1; then
  gh release create beta --title "Mione Alarmmelder Beta" --prerelease --notes "Automatisch erzeugter Beta-Release."
fi

while IFS= read -r asset_name_remote; do
  case "$asset_name_remote" in
    MioneAlarmmelder-*.zip)
      if [[ "$asset_name_remote" != "$asset_name" ]]; then
        gh release delete-asset beta "$asset_name_remote" -y
      fi
      ;;
  esac
done < <(gh release view beta --json assets --jq '.assets[].name' || true)

gh release upload beta "$asset_path" --clobber

git add "$assembly"
if git diff --cached --quiet -- "$assembly"; then
  echo "Keine Versionsänderung zum Committen gefunden."
else
  git commit -m "Bump beta to $release_version"
  git push origin "$(git branch --show-current)"
fi

echo
echo "Fertig: $asset_name"
