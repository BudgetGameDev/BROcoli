#!/usr/bin/env bash
# Promote one of Kenney's cursors into a pointer slot the game loads at runtime.
#
# The whole 182-cursor pack is committed under the game package's Cursors~/ folder. Unity
# ignores a folder whose name ends in a tilde, so the pack costs the project nothing to
# keep: no imports, no meta files, and nothing in a build. Only the two promoted slots
# live under Resources/, and only those two are built into the player.
#
# Copying a cursor into a slot is the whole switch. The game measures where the new art
# points, so no hotspot has to be worked out by hand.
#
#   ./scripts/select-cursor.sh outline gauntlet_point Gauntlet
#   ./scripts/select-cursor.sh basic cursor_none Steel
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "$0")/.." && pwd)"
PACKAGE="$PROJECT_PATH/LocalPackages/com.budgetgamedev.game.brocoli"
PACK="$PACKAGE/Cursors~/KenneyCursorPack"
SLOTS="$PACKAGE/Resources/Brocoli/Cursors"

usage() {
    cat >&2 <<'USAGE'
usage: select-cursor.sh <basic|outline> <cursor-name> <Steel|Gauntlet>

  basic|outline  which of Kenney's two styles to take the cursor from
  cursor-name    a cursor's file name without .png, e.g. cursor_none, gauntlet_point
  Steel|Gauntlet which pointer slot to overwrite; BrocoliPointer.ActiveStyle chooses
                 between the slots at runtime

Run with no arguments to list the cursors available.
USAGE
}

if [ "$#" -eq 0 ]; then
    usage
    echo >&2
    echo "available cursors:" >&2
    ls "$PACK/Outline" | sed 's/\.png$//' | column -c 100 >&2
    exit 2
fi

if [ "$#" -ne 3 ]; then
    usage
    exit 2
fi

case "$1" in
    basic) STYLE_DIR="Basic" ;;
    outline) STYLE_DIR="Outline" ;;
    *) usage; exit 2 ;;
esac

case "$3" in
    Steel) SLOT="PointerSteel" ;;
    Gauntlet) SLOT="PointerGauntlet" ;;
    *) usage; exit 2 ;;
esac

SOURCE="$PACK/$STYLE_DIR/$2.png"
if [ ! -f "$SOURCE" ]; then
    echo "select-cursor: no cursor named '$2' in $STYLE_DIR" >&2
    exit 1
fi

cp "$SOURCE" "$SLOTS/$SLOT.png"
echo "select-cursor: $STYLE_DIR/$2.png -> Resources/Brocoli/Cursors/$SLOT.png"
echo "select-cursor: nothing else to change; the game measures where the new art points."
