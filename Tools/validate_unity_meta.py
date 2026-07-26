#!/usr/bin/env python3
"""Validate Unity .meta files in the Apex package."""

from __future__ import annotations

import re
import sys
from pathlib import Path

PACKAGE_ROOT = Path("Packages/com.pancakedevs.apexphysics")
GUID_PATTERN = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)


def main() -> int:
    if not PACKAGE_ROOT.is_dir():
        print(f"Package folder not found: {PACKAGE_ROOT}")
        return 1

    errors: list[str] = []
    guid_owners: dict[str, Path] = {}

    for meta_path in sorted(PACKAGE_ROOT.rglob("*.meta")):
        raw = meta_path.read_bytes()

        if not raw.endswith(b"\n"):
            errors.append(f"{meta_path}: missing final newline")

        try:
            text = raw.decode("utf-8")
        except UnicodeDecodeError as exc:
            errors.append(f"{meta_path}: not valid UTF-8 ({exc})")
            continue

        match = GUID_PATTERN.search(text)
        if match is None:
            errors.append(f"{meta_path}: missing or malformed 32-character lowercase GUID")
            continue

        guid = match.group(1)
        previous_owner = guid_owners.get(guid)
        if previous_owner is not None:
            errors.append(f"{meta_path}: duplicate GUID {guid} also used by {previous_owner}")
        else:
            guid_owners[guid] = meta_path

        asset_path = Path(str(meta_path)[:-5])
        if not asset_path.exists():
            errors.append(f"{meta_path}: corresponding asset is missing ({asset_path})")

    if errors:
        print("Unity metadata validation failed:")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"Validated {len(guid_owners)} Unity metadata files successfully.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
