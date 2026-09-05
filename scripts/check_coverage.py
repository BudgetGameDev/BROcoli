#!/usr/bin/env python3
"""Enforce 100% line coverage of the game runtime, ratcheting down legacy gaps."""

import sys
from collections import defaultdict
from pathlib import Path
from xml.etree import ElementTree

ROOT = Path(__file__).resolve().parent.parent
BASELINE = ROOT / ".quality" / "coverage-baseline.tsv"
PACKAGES = ROOT / "LocalPackages"

# The shipping game runtime. Editor assemblies are authoring tooling and test
# assemblies are the measuring instrument, so neither is game code to cover.
MEASURED = {
    "BudgetGameDev.Shared": PACKAGES / "com.budgetgamedev.shared" / "Runtime",
    "BudgetGameDev.Hub": PACKAGES / "com.budgetgamedev.hub" / "Runtime",
    "BudgetGameDev.Games.Brocoli": PACKAGES / "com.budgetgamedev.game.brocoli" / "Runtime",
}

# Coverage is only honest if nothing can opt out of it. Suppressing a line is
# not a way to reach the target; making it reachable from a test is.
BANNED_ATTRIBUTES = ("ExcludeFromCodeCoverage", "ExcludeFromCoverage")


def source_files() -> list[Path]:
    files: set[Path] = set()
    for runtime_root in MEASURED.values():
        if runtime_root.exists():
            files.update(path for path in runtime_root.rglob("*.cs") if path.is_file())
    return sorted(files)


def load_baseline() -> dict[str, int]:
    baseline: dict[str, int] = {}
    for number, raw_line in enumerate(BASELINE.read_text(encoding="utf-8").splitlines(), 1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        count_text, separator, relative_path = line.partition("\t")
        if not separator or not count_text.isdigit() or not relative_path:
            raise ValueError(f"{BASELINE.relative_to(ROOT)}:{number}: expected '<lines>\\t<path>'")
        if relative_path in baseline:
            raise ValueError(f"{BASELINE.relative_to(ROOT)}:{number}: duplicate {relative_path}")
        if int(count_text) == 0:
            raise ValueError(
                f"{BASELINE.relative_to(ROOT)}:{number}: {relative_path} allows 0 uncovered "
                "lines; delete the entry instead"
            )
        baseline[relative_path] = int(count_text)
    return baseline


def read_report(report_dir: Path) -> tuple[dict[str, dict[int, bool]], set[str]]:
    """Map each measured source file to {line: covered}, plus the modules seen."""
    covered: dict[str, dict[int, bool]] = defaultdict(dict)
    modules: set[str] = set()

    for xml_path in sorted(report_dir.rglob("*.xml")):
        try:
            # The report is written by this repository's own Unity run, not
            # supplied by a caller, so the untrusted-XML rule does not apply.
            root = ElementTree.parse(xml_path).getroot()  # noqa: S314
        except ElementTree.ParseError as error:
            raise ValueError(f"{xml_path}: unreadable coverage report ({error})") from error
        if root.tag != "CoverageSession":
            continue

        for module in root.iter("Module"):
            if module.get("skippedDueTo"):
                continue
            name = module.findtext("ModuleName") or ""
            if name not in MEASURED:
                continue
            modules.add(name)

            paths = {
                entry.get("uid"): entry.get("fullPath") or ""
                for entry in module.iter("File")
                if entry.get("uid")
            }
            for point in module.iter("SequencePoint"):
                full_path = paths.get(point.get("fileid"))
                if not full_path:
                    continue
                relative = relative_to_root(full_path)
                if not relative:
                    continue
                line = int(point.get("sl") or 0)
                visited = int(point.get("vc") or 0) > 0
                if line:
                    covered[relative][line] = covered[relative].get(line, False) or visited

    return covered, modules


def relative_to_root(full_path: str) -> str:
    """Repo-relative path for a measured runtime source file, else an empty string.

    Both sides are resolved before comparison: the report records real paths, so
    a repository reached through a symlink would otherwise match nothing.
    """
    try:
        resolved = Path(full_path).resolve()
    except OSError:
        return ""
    for runtime_root in MEASURED.values():
        try:
            resolved.relative_to(runtime_root.resolve())
        except (OSError, ValueError):
            continue
        return resolved.relative_to(ROOT.resolve()).as_posix()
    return ""


def find_banned_attributes() -> list[str]:
    offenders: list[str] = []
    for path in source_files():
        text = path.read_text(encoding="utf-8", errors="replace")
        for number, line in enumerate(text.splitlines(), 1):
            stripped = line.strip()
            if not stripped.startswith("["):
                continue
            for attribute in BANNED_ATTRIBUTES:
                if attribute in stripped:
                    relative = path.relative_to(ROOT).as_posix()
                    offenders.append(f"{relative}:{number}: [{attribute}] is not allowed")
    return offenders


def uncovered_counts(report_dir: Path) -> tuple[dict[str, int], list[str]]:
    """Uncovered line counts per measured file, and any structural problems."""
    covered, modules = read_report(report_dir)
    problems = [
        f"assembly {name} is missing from the coverage report; it was not measured"
        for name in sorted(set(MEASURED) - modules)
    ]

    counts: dict[str, int] = {}
    for path in source_files():
        relative = path.relative_to(ROOT).as_posix()
        lines = covered.get(relative)
        # Every measured assembly was checked above, so a file the report says
        # nothing about contributed no sequence points: an interface, an enum,
        # an assembly attribute, an empty marker component. There is no
        # executable line in it to cover, and none to hold against it.
        counts[relative] = sum(1 for visited in (lines or {}).values() if not visited)
    return counts, problems


def write_baseline(counts: dict[str, int]) -> None:
    entries = sorted((path, count) for path, count in counts.items() if count > 0)
    lines = [
        "# Uncovered lines in the game runtime when the coverage gate was introduced.",
        "# Entries may only decrease. Remove an entry once its file reaches 100%.",
        "# Regenerate with: python3 scripts/check_coverage.py <report-dir> --write-baseline",
    ]
    lines.extend(f"{count}\t{path}" for path, count in entries)
    BASELINE.write_text("\n".join(lines) + "\n", encoding="utf-8")
    total = sum(count for _, count in entries)
    print(f"coverage: wrote {len(entries)} baseline entries ({total} uncovered lines)")


def report(counts: dict[str, int], baseline: dict[str, int], problems: list[str]) -> int:
    failures = list(problems)
    debt: list[str] = []

    for relative, uncovered in sorted(counts.items()):
        allowance = baseline.get(relative)
        if uncovered == 0:
            if allowance is not None:
                failures.append(f"{relative}: now fully covered; remove its baseline entry")
            continue
        if allowance is None:
            failures.append(f"{relative}: {uncovered} uncovered lines; every line must be covered")
        elif uncovered > allowance:
            failures.append(
                f"{relative}: {uncovered} uncovered lines, up from the {allowance} allowed"
            )
        else:
            debt.append(relative)

    failures.extend(
        f"{relative}: baseline entry is stale because the file is missing"
        for relative in sorted(set(baseline) - set(counts))
    )

    measured = sum(1 for _ in counts)
    remaining = sum(counts.values())
    if failures:
        print("coverage: FAIL (100% line coverage of the game runtime)", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        return 1

    print(
        f"coverage: PASS ({measured} runtime files; "
        f"{remaining} uncovered lines across {len(debt)} ratcheted files)"
    )
    return 0


def main(argv: list[str]) -> int:
    write = "--write-baseline" in argv
    positional = [argument for argument in argv if not argument.startswith("--")]
    if len(positional) != 1 or len(argv) != len(positional) + (1 if write else 0):
        print(
            "Usage: check_coverage.py <report-dir> [--write-baseline]",
            file=sys.stderr,
        )
        return 2

    report_dir = Path(positional[0])
    if not report_dir.is_dir():
        print(f"coverage: no coverage report directory at {report_dir}", file=sys.stderr)
        return 1

    banned = find_banned_attributes()
    if banned:
        print("coverage: FAIL (coverage suppression is not allowed)", file=sys.stderr)
        for offender in banned:
            print(f"  {offender}", file=sys.stderr)
        return 1

    try:
        counts, problems = uncovered_counts(report_dir)
        if write:
            if problems:
                print(
                    "coverage: refusing to write a baseline from an incomplete report",
                    file=sys.stderr,
                )
                for problem in problems:
                    print(f"  {problem}", file=sys.stderr)
                return 1
            write_baseline(counts)
            return 0
        baseline = load_baseline()
    except (OSError, ValueError) as error:
        print(f"coverage: {error}", file=sys.stderr)
        return 1

    return report(counts, baseline, problems)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
