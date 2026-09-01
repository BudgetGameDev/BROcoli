#!/usr/bin/env bash
# Shared reading and verification of packaged native player artifacts.
# Source this file; it defines native_artifacts_field, native_artifacts_assets,
# and native_artifacts_verify.

# Prints one key from the packaged build-info.txt.
native_artifacts_field() {
    local artifacts_root="$1"
    local key="$2"

    awk -F= -v key="$key" '$1 == key { print $2 }' "$artifacts_root/build-info.txt"
}

# Prints the artifact paths the recorded build actually produced, one per line.
native_artifacts_assets() {
    local artifacts_root="$1"
    local targets
    local target

    targets="$(native_artifacts_field "$artifacts_root" targets)"
    if [ -z "$targets" ]; then
        echo "native-artifacts: build-info records no targets; rebuild" >&2
        return 1
    fi

    local IFS=,
    for target in $targets; do
        case "$target" in
            windows) echo "$artifacts_root/BROcoli-windows-x86_64.zip" ;;
            macos) echo "$artifacts_root/BROcoli-macos-universal.zip" ;;
            linux) echo "$artifacts_root/BROcoli-linux-x86_64.tar.gz" ;;
            *)
                echo "native-artifacts: build-info records unknown target '$target'" >&2
                return 1
                ;;
        esac
    done
    echo "$artifacts_root/SHA256SUMS"
    echo "$artifacts_root/build-info.txt"
}

# Fails unless every recorded artifact exists, matches its checksum, and came
# from the given commit with a clean tree. Prints the asset paths on success.
native_artifacts_verify() {
    local artifacts_root="$1"
    local expected_commit="$2"
    local asset
    local assets=()

    if [ ! -f "$artifacts_root/build-info.txt" ]; then
        echo "native-artifacts: no packaged build in $artifacts_root" >&2
        return 1
    fi

    while IFS= read -r asset; do
        assets+=("$asset")
    done < <(native_artifacts_assets "$artifacts_root")
    if [ "${#assets[@]}" -eq 0 ]; then
        echo "native-artifacts: build-info records no artifacts" >&2
        return 1
    fi

    for asset in "${assets[@]}"; do
        if [ ! -f "$asset" ]; then
            echo "native-artifacts: missing artifact '$asset'" >&2
            return 1
        fi
    done
    # Keep the checker's own chatter off stdout; callers read asset paths there.
    (
        cd "$artifacts_root"
        shasum -a 256 -c SHA256SUMS >&2
    ) || return 1

    local build_commit
    local build_dirty
    build_commit="$(native_artifacts_field "$artifacts_root" commit)"
    build_dirty="$(native_artifacts_field "$artifacts_root" dirty)"
    if [ "$build_commit" != "$expected_commit" ] || [ "$build_dirty" != "false" ]; then
        echo "native-artifacts: artifacts are not from this clean commit" >&2
        return 1
    fi

    printf '%s\n' "${assets[@]}"
}
