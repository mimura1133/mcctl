#!/usr/bin/env python3
"""Generate Strings.resx (English, neutral) and Strings.ja.resx from Localization/strings.tsv.

Usage:
    python tools/gen_resx.py src/Mcctl.App/Localization

TSV columns: key <TAB> Japanese <TAB> English. Lines starting with '#' and blank lines are ignored.
"""

from __future__ import annotations

import sys
from pathlib import Path
from xml.sax.saxutils import escape

HEADER = """<?xml version="1.0" encoding="utf-8"?>
<!-- Generated from strings.tsv by tools/gen_resx.py. Edit the TSV, not this file. -->
<root>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
"""


def read_table(tsv: Path) -> list[tuple[str, str, str]]:
    """Returns (key, japanese, english) rows, rejecting malformed lines and duplicate keys."""
    rows: list[tuple[str, str, str]] = []
    seen: set[str] = set()
    for number, line in enumerate(tsv.read_text(encoding="utf-8").splitlines(), start=1):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        parts = line.split("\t")
        if len(parts) != 3 or not parts[0]:
            raise SystemExit(f"{tsv}:{number}: expected 'key<TAB>ja<TAB>en': {line!r}")
        key, ja, en = parts
        if key in seen:
            raise SystemExit(f"{tsv}:{number}: duplicate key {key!r}")
        seen.add(key)
        rows.append((key, ja, en))
    return rows


def write_resx(path: Path, entries: list[tuple[str, str]]) -> None:
    body = "".join(
        f'  <data name="{key}" xml:space="preserve"><value>{escape(value)}</value></data>\n'
        for key, value in entries
    )
    # newline="\n" keeps the output identical on Windows and Linux.
    path.write_text(HEADER + body + "</root>\n", encoding="utf-8", newline="\n")


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__.strip(), file=sys.stderr)
        return 2

    directory = Path(argv[1])
    rows = read_table(directory / "strings.tsv")
    write_resx(directory / "Strings.resx", [(key, en) for key, _, en in rows])
    write_resx(directory / "Strings.ja.resx", [(key, ja) for key, ja, _ in rows])
    print(f"wrote {len(rows)} strings")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
