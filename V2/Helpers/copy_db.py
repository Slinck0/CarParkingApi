import sqlite3

OLD_DB = "backup_parking.db"
NEW_DB = "parking.db"

def table_names(conn):
    cur = conn.execute("""
        SELECT name FROM sqlite_master
        WHERE type='table' AND name NOT LIKE 'sqlite_%'
    """)
    return [r[0] for r in cur.fetchall()]

def columns(conn, table):
    cur = conn.execute(f"PRAGMA table_info('{table}')")
    return [r[1] for r in cur.fetchall()]

old = sqlite3.connect(OLD_DB)
new = sqlite3.connect(NEW_DB)

old.execute("PRAGMA foreign_keys=OFF;")
new.execute("PRAGMA foreign_keys=OFF;")

old_tables = set(table_names(old))
new_tables = set(table_names(new))

for t in sorted(old_tables):
    if t not in new_tables:
        print(f"SKIP (not in new db): {t}")
        continue

    old_cols_order = columns(old, t)
    new_cols = set(columns(new, t))
    common = [c for c in old_cols_order if c in new_cols]

    if not common:
        print(f"SKIP (no common columns): {t}")
        continue

    col_list = ", ".join([f'"{c}"' for c in common])
    placeholders = ", ".join(["?"] * len(common))

    rows = old.execute(f'SELECT {col_list} FROM "{t}"').fetchall()

    new.execute(f'DELETE FROM "{t}"')
    new.executemany(
        f'INSERT INTO "{t}" ({col_list}) VALUES ({placeholders})',
        rows
    )
    new.commit()
    print(f"COPIED {t}: {len(rows)} rows")

new.execute("PRAGMA foreign_keys=ON;")
new.commit()
old.close()
new.close()

print("Done.")
