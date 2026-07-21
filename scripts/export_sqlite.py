"""
Export specified SQLite tables to CSV files.

Usage (from repo root):
    python scripts/export_sqlite.py
    python scripts/export_sqlite.py --db-path ./backend/workhour.db --output-dir ./exports
    python scripts/export_sqlite.py --tables WorkHours NcmTimes Products Orders
    python scripts/export_sqlite.py --tables WorkHours,NcmTimes,Products,Orders
    python scripts/export_sqlite.py --db-path E:/MISCMFactoryService/backend/workhour.db --tables WorkHours,NcmTimes,Products,Orders --output-dir ./exports

Behavior:
- Tries to read backend/appsettings.json (DbPath, DbProvider) to find DB file; falls back to backend/workhour.db.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import os
import sqlite3
import sys
from pathlib import Path


DEFAULT_TABLES = ["WorkHours", "NcmTimes", "Products","Orders"]


def resolve_repo_root(start: Path) -> Path:
    """Walk up until WorkHourTool.sln is found, else return start."""
    start = start.resolve()
    cur = start
    while True:
        if (cur / "WorkHourTool.sln").exists():
            return cur
        parent = cur.parent
        if parent == cur:
            return start
        cur = parent


def get_default_db_path(repo_root: Path) -> Path:
    backend_dir = repo_root / "backend"
    appsettings = backend_dir / "appsettings.json"
    if appsettings.exists():
        try:
            data = json.loads(appsettings.read_text(encoding="utf-8"))
            cfg_path = data.get("DbPath")
            provider = (data.get("DbProvider") or "sqlite").strip().lower()
            if provider != "sqlite":
                print(f"[warn] DbProvider is '{provider}', this script expects SQLite.")
            if cfg_path:
                p = Path(cfg_path)
                return p if p.is_absolute() else (backend_dir / p)
        except Exception as e:
            print(f"[warn] Failed to read appsettings.json, falling back to default path: {e}")
    return backend_dir / "workhour.db"


def ensure_dir(p: Path) -> None:
    p.mkdir(parents=True, exist_ok=True)


def export_table(conn: sqlite3.Connection, table: str, out_csv: Path) -> None:
    table_quoted = table.replace('"', '""')
    sql = f"SELECT * FROM \"{table_quoted}\";"
    cur = conn.cursor()
    try:
        cur.execute(sql)
    except sqlite3.Error as e:
        raise RuntimeError(f"query failed for table '{table}': {e}") from e

    cols = [d[0] for d in cur.description or []]
    rows = cur.fetchall()

    # Write CSV with headers. Use utf-8-sig so Windows Notepad recognizes UTF-8 (writes BOM).
    # Normalize row values: convert bytes to text, replace None with empty string.
    def normalize_value(v):
        if v is None:
            return ''
        if isinstance(v, (bytes, bytearray)):
            try:
                return v.decode('utf-8')
            except Exception:
                return v.decode('utf-8', errors='replace')
        return v

    with out_csv.open("w", newline="", encoding="utf-8-sig", errors="replace") as f:
        writer = csv.writer(f)
        writer.writerow(cols)
        for row in rows:
            writer.writerow([normalize_value(v) for v in row])


def parse_tables(values: list[str] | None) -> list[str]:
    if not values:
        return list(DEFAULT_TABLES)
    result: list[str] = []
    for v in values:
        if "," in v:
            result.extend([x.strip() for x in v.split(",") if x.strip()])
        else:
            v = v.strip()
            if v:
                result.append(v)
    return result or list(DEFAULT_TABLES)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Export SQLite tables to CSV.")
    parser.add_argument("--db-path", dest="db_path", default=None, help="Path to SQLite DB file. Defaults to backend/appsettings.json DbPath or backend/workhour.db")
    parser.add_argument("--output-dir", dest="output_dir", default="./exports", help="Directory to write exports (a timestamped subdir will be created)")
    parser.add_argument("--tables", nargs="*", default=None, help="Tables to export (space or comma separated). Defaults to WorkHours NcmTimes Products Orders")

    args = parser.parse_args(argv)

    repo_root = resolve_repo_root(Path.cwd())
    db_path = Path(args.db_path) if args.db_path else get_default_db_path(repo_root)
    if not db_path.exists():
        print(f"[error] Database file not found: {db_path}")
        return 1

    tables = parse_tables(args.tables)
    # Write directly into the provided output directory (no timestamped subfolder)
    out_dir = Path(args.output_dir)
    ensure_dir(out_dir)

    print(f"Exporting from: {db_path}")
    print(f"Output folder: {out_dir}")

    # Connect using SQLite; disable type detection to keep raw text formatting
    try:
        conn = sqlite3.connect(str(db_path))
    except sqlite3.Error as e:
        print(f"[error] Failed to open database: {e}")
        return 1

    failures: list[str] = []
    with conn:
        for t in tables:
            if not t:
                continue
            table = t.strip()
            if not table:
                continue
            out_csv = out_dir / f"{table}.csv"
            # Use ASCII arrow to avoid Unicode encoding errors when running under Windows Task Scheduler
            print(f"  -> {table}", end="")
            try:
                export_table(conn, table, out_csv)
            except Exception as e:
                print("  [FAILED]")
                failures.append(f"{table}: {e}")
                continue
            print(f"  [OK] -> {out_csv.name}")

    if failures:
        print("\nSome tables failed to export:")
        for msg in failures:
            print(f" - {msg}")
        return 2

    print("\nExport completed successfully.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
