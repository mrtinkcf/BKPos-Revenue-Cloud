CREATE TABLE IF NOT EXISTS inventory_daily_stock (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    business_date TEXT NOT NULL,
    product_id TEXT NOT NULL,
    product_name TEXT NOT NULL DEFAULT '',
    unit_name TEXT NOT NULL DEFAULT '',
    opening_qty REAL NOT NULL DEFAULT 0,
    import_qty REAL NOT NULL DEFAULT 0,
    last_import_price INTEGER NOT NULL DEFAULT 0,
    manual_export_qty REAL NOT NULL DEFAULT 0,
    sold_qty REAL NOT NULL DEFAULT 0,
    total_export_qty REAL NOT NULL DEFAULT 0,
    closing_qty REAL NOT NULL DEFAULT 0,
    min_stock REAL NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (tenant_id, store_id, business_date, product_id)
);

CREATE INDEX IF NOT EXISTS idx_inventory_daily_store_date
    ON inventory_daily_stock (tenant_id, store_id, business_date);

CREATE INDEX IF NOT EXISTS idx_inventory_daily_product
    ON inventory_daily_stock (tenant_id, store_id, product_id, product_name);
