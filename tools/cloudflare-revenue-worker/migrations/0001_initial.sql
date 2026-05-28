-- BKPos Revenue Cloud V1 D1 schema

CREATE TABLE IF NOT EXISTS tenants (
    tenant_id TEXT PRIMARY KEY,
    name TEXT NOT NULL DEFAULT '',
    enabled INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE TABLE IF NOT EXISTS stores (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    name TEXT NOT NULL DEFAULT '',
    timezone TEXT NOT NULL DEFAULT 'Asia/Ho_Chi_Minh',
    revenue_cloud_enabled INTEGER NOT NULL DEFAULT 1,
    last_heartbeat_at TEXT,
    last_sync_at TEXT,
    last_error TEXT,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (tenant_id, store_id),
    FOREIGN KEY (tenant_id) REFERENCES tenants(tenant_id)
);

CREATE TABLE IF NOT EXISTS store_sync_keys (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    key_id TEXT NOT NULL,
    sync_key TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    revoked_at TEXT,
    PRIMARY KEY (tenant_id, store_id, key_id),
    FOREIGN KEY (tenant_id, store_id) REFERENCES stores(tenant_id, store_id)
);

CREATE TABLE IF NOT EXISTS manager_users (
    user_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    username TEXT NOT NULL,
    display_name TEXT NOT NULL DEFAULT '',
    password_hash TEXT NOT NULL,
    password_salt TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    UNIQUE (tenant_id, username),
    FOREIGN KEY (tenant_id) REFERENCES tenants(tenant_id)
);

CREATE TABLE IF NOT EXISTS manager_sessions (
    session_id TEXT PRIMARY KEY,
    user_id TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    access_token_hash TEXT NOT NULL UNIQUE,
    refresh_token_hash TEXT NOT NULL UNIQUE,
    access_expires_at TEXT NOT NULL,
    refresh_expires_at TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    last_seen_at TEXT,
    revoked_at TEXT,
    FOREIGN KEY (user_id) REFERENCES manager_users(user_id)
);

CREATE TABLE IF NOT EXISTS sync_nonces (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    nonce TEXT NOT NULL,
    created_at TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    PRIMARY KEY (tenant_id, store_id, nonce)
);

CREATE TABLE IF NOT EXISTS sync_logs (
    log_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL DEFAULT '',
    store_id TEXT NOT NULL DEFAULT '',
    event_type TEXT NOT NULL,
    message TEXT NOT NULL DEFAULT '',
    body_sha256 TEXT NOT NULL DEFAULT '',
    request_timestamp TEXT NOT NULL DEFAULT '',
    request_nonce TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE TABLE IF NOT EXISTS invoices (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    invoice_id TEXT NOT NULL,
    invoice_version INTEGER NOT NULL DEFAULT 1,
    status TEXT NOT NULL,
    table_name TEXT NOT NULL DEFAULT '',
    cashier TEXT NOT NULL DEFAULT '',
    opened_at TEXT,
    paid_at TEXT,
    business_date TEXT NOT NULL,
    subtotal INTEGER NOT NULL DEFAULT 0,
    discount INTEGER NOT NULL DEFAULT 0,
    total INTEGER NOT NULL DEFAULT 0,
    payment_method TEXT NOT NULL DEFAULT 'other',
    discount_note TEXT NOT NULL DEFAULT '',
    modified_at TEXT,
    synced_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    raw_payload TEXT,
    PRIMARY KEY (tenant_id, store_id, invoice_id)
);

CREATE TABLE IF NOT EXISTS invoice_items (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    invoice_id TEXT NOT NULL,
    line_id TEXT NOT NULL,
    product_id TEXT NOT NULL DEFAULT '',
    product_name TEXT NOT NULL DEFAULT '',
    product_type TEXT NOT NULL DEFAULT 'drink',
    unit_name TEXT NOT NULL DEFAULT '',
    quantity REAL NOT NULL DEFAULT 0,
    unit_price INTEGER NOT NULL DEFAULT 0,
    line_total INTEGER NOT NULL DEFAULT 0,
    note TEXT NOT NULL DEFAULT '',
    PRIMARY KEY (tenant_id, store_id, invoice_id, line_id)
);

CREATE TABLE IF NOT EXISTS invoice_payment_lines (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    invoice_id TEXT NOT NULL,
    payment_id TEXT NOT NULL,
    method TEXT NOT NULL DEFAULT 'other',
    amount INTEGER NOT NULL DEFAULT 0,
    created_at TEXT,
    PRIMARY KEY (tenant_id, store_id, invoice_id, payment_id)
);

CREATE TABLE IF NOT EXISTS open_tables (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    table_id TEXT NOT NULL,
    table_name TEXT NOT NULL DEFAULT '',
    zone_id TEXT NOT NULL DEFAULT '',
    zone_name TEXT NOT NULL DEFAULT '',
    order_id TEXT NOT NULL DEFAULT '',
    occupied_at TEXT,
    total INTEGER NOT NULL DEFAULT 0,
    modified_at TEXT,
    active INTEGER NOT NULL DEFAULT 1,
    synced_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (tenant_id, store_id, table_id)
);

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

CREATE TABLE IF NOT EXISTS daily_revenue_snapshots (
    tenant_id TEXT NOT NULL,
    store_id TEXT NOT NULL,
    business_date TEXT NOT NULL,
    revenue INTEGER NOT NULL DEFAULT 0,
    invoice_count INTEGER NOT NULL DEFAULT 0,
    cancelled_invoice_count INTEGER NOT NULL DEFAULT 0,
    average_invoice_value INTEGER NOT NULL DEFAULT 0,
    cash_amount INTEGER NOT NULL DEFAULT 0,
    transfer_amount INTEGER NOT NULL DEFAULT 0,
    card_amount INTEGER NOT NULL DEFAULT 0,
    other_amount INTEGER NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
    PRIMARY KEY (tenant_id, store_id, business_date)
);

CREATE TABLE IF NOT EXISTS audit_logs (
    audit_id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL DEFAULT '',
    store_id TEXT NOT NULL DEFAULT '',
    actor_type TEXT NOT NULL DEFAULT '',
    actor_id TEXT NOT NULL DEFAULT '',
    action TEXT NOT NULL,
    message TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);

CREATE INDEX IF NOT EXISTS idx_invoices_store_date ON invoices (tenant_id, store_id, business_date);
CREATE INDEX IF NOT EXISTS idx_invoices_status ON invoices (tenant_id, store_id, status);
CREATE INDEX IF NOT EXISTS idx_items_product ON invoice_items (tenant_id, store_id, product_id, product_name);
CREATE INDEX IF NOT EXISTS idx_payments_method ON invoice_payment_lines (tenant_id, store_id, method);
CREATE INDEX IF NOT EXISTS idx_open_tables_active ON open_tables (tenant_id, store_id, active);
CREATE INDEX IF NOT EXISTS idx_open_table_items_table ON open_table_items (tenant_id, store_id, table_id);
CREATE INDEX IF NOT EXISTS idx_sessions_user ON manager_sessions (tenant_id, user_id);
CREATE INDEX IF NOT EXISTS idx_nonces_expires ON sync_nonces (expires_at);
