CREATE TABLE IF NOT EXISTS open_table_items (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    table_id TEXT NOT NULL,
    order_id TEXT NOT NULL DEFAULT '',
    line_id TEXT NOT NULL,
    product_id TEXT NOT NULL DEFAULT '',
    product_name TEXT NOT NULL DEFAULT '',
    product_type TEXT NOT NULL DEFAULT 'other',
    unit_name TEXT NOT NULL DEFAULT '',
    quantity REAL NOT NULL DEFAULT 0,
    unit_price INTEGER NOT NULL DEFAULT 0,
    line_total INTEGER NOT NULL DEFAULT 0,
    note TEXT NOT NULL DEFAULT '',
    synced_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (tenant_id, store_id, table_id, line_id)
);

CREATE INDEX IF NOT EXISTS idx_open_table_items_table ON open_table_items (tenant_id, store_id, table_id);
