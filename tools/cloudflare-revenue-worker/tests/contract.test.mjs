import test from "node:test";
import assert from "node:assert/strict";
import worker, { __test } from "../worker.js";

const store = {
  tenant_id: "TENANT-001",
  store_id: "STORE-001",
  timezone: "Asia/Ho_Chi_Minh",
  last_sync_at: "2026-05-20T08:35:00+07:00"
};

test("GET /reports/today response matches contract", async () => {
  const response = await __test.reportToday(mockEnv(), store, "2026-05-20", "request");

  assert.equal(response.storeId, "STORE-001");
  assert.equal(response.timezone, "Asia/Ho_Chi_Minh");
  assert.equal(response.dateSource, "request");
  assert.equal(response.businessDate, "2026-05-20");
  assertSummary(response.summary);
  assertDaily(response.revenue7Days);
  assertPaymentBreakdown(response.paymentBreakdown);
});

test("GET /reports/range response includes paymentBreakdown", async () => {
  const response = await __test.reportRange(mockEnv(), store, "2026-05-01", "2026-05-20");

  assert.equal(response.from, "2026-05-01");
  assert.equal(response.to, "2026-05-20");
  assertSummary(response.summary);
  assertDaily(response.daily);
  assertPaymentBreakdown(response.paymentBreakdown);
});

test("GET /reports/month response matches contract", async () => {
  const response = await __test.reportMonth(mockEnv(), store, "2026-05");

  assert.equal(response.month, "2026-05");
  assert.equal(response.timezone, "Asia/Ho_Chi_Minh");
  assertSummary(response.summary);
  assertDaily(response.daily);
  assertPaymentBreakdown(response.paymentBreakdown);
});

test("GET /reports/top-products response matches contract", async () => {
  const response = await __test.topProducts(mockEnv(), store, "2026-05-01", "2026-05-20", 20);

  assert.equal(response.from, "2026-05-01");
  assert.equal(response.to, "2026-05-20");
  assert.ok(Array.isArray(response.items));
  assert.deepEqual(Object.keys(response.items[0]).sort(), ["productId", "productName", "productType", "quantity", "revenue"].sort());
});

test("GET /reports/open-tables response matches contract", async () => {
  const response = await __test.reportOpenTables(mockEnv(), store);

  assert.equal(response.storeId, "STORE-001");
  assert.equal(typeof response.tableCount, "number");
  assert.equal(typeof response.estimatedTotal, "number");
  assert.ok(Array.isArray(response.tables));
  assert.deepEqual(
    Object.keys(response.tables[0]).sort(),
    ["tableId", "tableName", "zoneId", "zoneName", "orderId", "occupiedAt", "total", "modifiedAt", "syncedAt", "items"].sort());
  assert.ok(Array.isArray(response.tables[0].items));
  assert.deepEqual(
    Object.keys(response.tables[0].items[0]).sort(),
    ["lineId", "productId", "productName", "productType", "unitName", "quantity", "unitPrice", "lineTotal", "note"].sort());
});

test("GET /reports/inventory response matches contract", async () => {
  const response = await __test.reportInventory(mockEnv(), store, new URLSearchParams("period=today"));

  assert.equal(response.storeId, "STORE-001");
  assert.equal(response.period, "today");
  assert.equal(typeof response.missingInventorySnapshot, "boolean");
  assert.equal(typeof response.snapshotDays, "number");
  assert.equal(typeof response.expectedSnapshotDays, "number");
  assert.deepEqual(
    Object.keys(response.summary).sort(),
    ["productCount", "lowStockCount", "totalImportQty", "totalSoldQty", "totalManualExportQty"].sort());
  assert.ok(Array.isArray(response.items));
  assert.deepEqual(
    Object.keys(response.items[0]).sort(),
    ["productId", "productName", "unitName", "openingQty", "importQty", "lastImportPrice", "manualExportQty", "soldQty", "totalExportQty", "closingQty", "minStock", "isLowStock"].sort());
});

test("GET /invoices list response matches contract", async () => {
  const response = await __test.invoiceList(mockEnv(), store, new URLSearchParams("from=2026-05-01&to=2026-05-20&page=1&pageSize=50"));

  assert.equal(response.page, 1);
  assert.equal(response.pageSize, 50);
  assert.equal(typeof response.totalItems, "number");
  assert.ok(Array.isArray(response.items));
  assert.deepEqual(
    Object.keys(response.items[0]).sort(),
    ["invoiceId", "invoiceVersion", "status", "businessDate", "tableName", "cashier", "paidAt", "subtotal", "discount", "total", "paymentMethod"].sort());
});

test("GET /invoices/{id} response includes header, payments and items", async () => {
  const response = await __test.invoiceDetail(mockEnv(), store, "INV-001");

  assert.equal(response.tenantId, "TENANT-001");
  assert.equal(response.storeId, "STORE-001");
  assert.equal(response.invoiceId, "INV-001");
  assert.equal(typeof response.invoiceVersion, "number");
  assert.ok(Array.isArray(response.payments));
  assert.ok(Array.isArray(response.items));
  assert.deepEqual(Object.keys(response.payments[0]).sort(), ["method", "amount", "createdAt"].sort());
  assert.deepEqual(
    Object.keys(response.items[0]).sort(),
    ["lineId", "productId", "productName", "productType", "unitName", "quantity", "unitPrice", "lineTotal", "note"].sort());
});

test("sync HMAC validates exact UTF-8 body bytes and logs failed canonical fields", async () => {
  const secret = "sync-secret-for-contract-test";
  const env = mockEnv({ syncKey: secret });
  const timestamp = String(Math.floor(Date.now() / 1000));
  const okNonce = "9b31e8c3-0891-4a4e-9d30-2e9b97c1f1a5";
  const okBody = '{"tenantId":"TENANT-001","storeId":"STORE-001"}';

  const okResponse = await worker.fetch(new Request("https://worker.test/sync/heartbeat", {
    method: "POST",
    headers: await signedHeaders(secret, timestamp, okNonce, okBody),
    body: okBody
  }), env);

  assert.equal(okResponse.status, 200);

  const badNonce = "a4421771-d169-4ffe-bccb-22d0455ac9f4";
  const prettyBody = '{\n  "tenantId": "TENANT-001",\n  "storeId": "STORE-001"\n}';
  const badResponse = await worker.fetch(new Request("https://worker.test/sync/heartbeat", {
    method: "POST",
    headers: await signedHeaders(secret, timestamp, badNonce, okBody),
    body: prettyBody
  }), env);

  assert.equal(badResponse.status, 401);
  assert.equal((await badResponse.json()).error, "invalid_signature");
  assert.ok(env.DB.syncLogs.some(x => x.eventType === "invalid_signature" && x.message === `ts=${timestamp};nonce=${badNonce}`));
  assert.ok(env.DB.syncLogs.some(x => x.requestTimestamp === timestamp && x.requestNonce === badNonce && /^[0-9a-f]{64}$/.test(x.bodySha256)));
});

test("sync inventory-daily upserts stock snapshot rows", async () => {
  const env = mockEnv();
  const response = await __test.syncInventoryDaily(env, { tenantId: "TENANT-001", storeId: "STORE-001" }, {
    businessDate: "2026-06-08",
    items: [
      {
        productId: "BIA-HENIKEN",
        productName: "BIA HENIKEN LON",
        unitName: "Lon",
        openingQty: 100,
        importQty: 0,
        lastImportPrice: 18000,
        manualExportQty: 0,
        soldQty: 20,
        closingQty: 80,
        minStock: 5
      }
    ]
  });

  assert.equal(response.ok, true);
  assert.equal(response.businessDate, "2026-06-08");
  assert.equal(response.accepted, 1);
  assert.equal(response.failed, 0);
  assert.equal(env.DB.inventoryRows.length, 1);
  assert.equal(env.DB.inventoryRows[0].productId, "BIA-HENIKEN");
  assert.equal(env.DB.inventoryRows[0].soldQty, 20);
  assert.equal(env.DB.inventoryRows[0].totalExportQty, 20);
});

test("admin delete-data clears report data for a tenant store", async () => {
  const env = { ...mockEnv(), ADMIN_SECRET: "admin-secret" };
  const response = await worker.fetch(new Request("https://worker.test/admin/revenue-cloud/delete-data", {
    method: "POST",
    headers: {
      "authorization": "Bearer admin-secret",
      "content-type": "application/json; charset=utf-8"
    },
    body: JSON.stringify({
      tenantId: "TENANT-001",
      storeId: "STORE-001",
      confirm: "DELETE_REVENUE_CLOUD_DATA"
    })
  }), env);

  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.ok, true);
  assert.equal(body.tenantId, "TENANT-001");
  assert.equal(body.storeId, "STORE-001");
  assert.equal(body.totalDeleted, 9);
  assert.deepEqual(env.DB.deletedTables, [
    "invoice_payment_lines",
    "invoice_items",
    "invoices",
    "open_table_items",
    "open_tables",
    "inventory_daily_stock",
    "daily_revenue_snapshots",
    "sync_nonces",
    "sync_logs"
  ]);
  assert.equal(env.DB.auditLogs.at(-1)?.action, "delete_revenue_cloud_data");
});

function assertSummary(summary) {
  assert.deepEqual(
    Object.keys(summary).sort(),
    ["revenue", "invoiceCount", "cancelledInvoiceCount", "averageInvoiceValue", "cashAmount", "transferAmount", "cardAmount", "otherAmount"].sort());
  for (const value of Object.values(summary)) {
    assert.equal(typeof value, "number");
  }
}

function assertDaily(rows) {
  assert.ok(Array.isArray(rows));
  assert.deepEqual(Object.keys(rows[0]).sort(), ["date", "revenue", "invoiceCount"].sort());
}

function assertPaymentBreakdown(rows) {
  assert.ok(Array.isArray(rows));
  assert.deepEqual(rows.map(x => x.method), ["cash", "transfer", "card", "other"]);
  for (const row of rows) {
    assert.equal(typeof row.amount, "number");
  }
}

async function signedHeaders(secret, timestamp, nonce, bodyText) {
  return {
    "content-type": "application/json; charset=utf-8",
    "x-bkpos-tenant": "TENANT-001",
    "x-bkpos-store": "STORE-001",
    "x-bkpos-timestamp": timestamp,
    "x-bkpos-nonce": nonce,
    "x-bkpos-signature": await sign(secret, timestamp, nonce, bodyText)
  };
}

async function sign(secret, timestamp, nonce, bodyText) {
  const encoder = new TextEncoder();
  const prefix = encoder.encode(`${timestamp}|${nonce}|`);
  const body = encoder.encode(bodyText);
  const message = new Uint8Array(prefix.length + body.length);
  message.set(prefix);
  message.set(body, prefix.length);
  const key = await crypto.subtle.importKey("raw", encoder.encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const bytes = new Uint8Array(await crypto.subtle.sign("HMAC", key, message));
  return [...bytes].map(b => b.toString(16).padStart(2, "0")).join("");
}

function mockEnv(options = {}) {
  return { DB: new MockD1(options) };
}

class MockD1 {
  constructor(options = {}) {
    this.syncKey = options.syncKey || "SYNC-KEY";
    this.nonces = new Set();
    this.syncLogs = [];
    this.auditLogs = [];
    this.deletedTables = [];
    this.inventoryRows = [];
  }

  prepare(sql) {
    return new MockStatement(sql, this);
  }

  async batch(statements) {
    return Promise.all(statements.map(statement => statement.run()));
  }
}

class MockStatement {
  constructor(sql, db) {
    this.sql = sql;
    this.db = db;
    this.params = [];
  }

  bind(...params) {
    this.params = params;
    return this;
  }

  async first() {
    const sql = normalize(this.sql);
    if (sql.includes("select s.*,t.enabled as tenant_enabled from stores")) {
      return { ...store, tenant_enabled: 1, revenue_cloud_enabled: 1 };
    }

    if (sql.includes("select count(*) count from invoices")) {
      return { count: 1 };
    }

    if (sql.includes("count(distinct business_date)") && sql.includes("from inventory_daily_stock")) {
      return { snapshot_days: 1, updated_at: "2026-06-08T10:00:00+07:00" };
    }

    if (sql.includes("select * from invoices")) {
      return invoiceRow();
    }

    if (sql.includes("from invoices where") && sql.includes("invoice_count")) {
      return { revenue: 900000, invoice_count: 10, cancelled_invoice_count: 1 };
    }

    throw new Error(`Unhandled first SQL: ${this.sql}`);
  }

  async all() {
    const sql = normalize(this.sql);
    if (sql.includes("select sync_key from store_sync_keys")) {
      return { results: [{ sync_key: this.db.syncKey }] };
    }

    if (sql.includes("from daily_revenue_snapshots")) {
      return { results: [{ business_date: "2026-05-20", revenue: 900000, invoice_count: 10 }] };
    }

    if (sql.includes("from invoice_payment_lines p join invoices")) {
      return {
        results: [
          { method: "cash", amount: 500000 },
          { method: "transfer", amount: 300000 },
          { method: "card", amount: 100000 }
        ]
      };
    }

    if (sql.includes("from invoice_items it join invoices")) {
      return {
        results: [
          { product_id: "B52", product_name: "B52", product_type: "drink", quantity: 20, revenue: 600000 }
        ]
      };
    }

    if (sql.includes("select invoice_id,invoice_version,status")) {
      return { results: [invoiceRow()] };
    }

    if (sql.includes("from open_tables where")) {
      return {
        results: [
          {
            table_id: "A1",
            table_name: "Bàn A1",
            zone_id: "KHU-A",
            zone_name: "Khu A",
            order_id: "ORD-001",
            occupied_at: "2026-05-20T08:00:00+07:00",
            total: 125000,
            modified_at: "2026-05-20T08:15:00+07:00",
            synced_at: "2026-05-20T08:16:00+07:00"
          }
        ]
      };
    }

    if (sql.includes("from open_table_items")) {
      return {
        results: [
          {
            table_id: "A1",
            order_id: "ORD-001",
            line_id: "LINE-001",
            product_id: "B52",
            product_name: "B52",
            product_type: "drink",
            unit_name: "ly",
            quantity: 1,
            unit_price: 30000,
            line_total: 30000,
            note: "ít đá"
          }
        ]
      };
    }

    if (sql.includes("from invoice_items where")) {
      return {
        results: [
          {
            line_id: "LINE-001",
            product_id: "B52",
            product_name: "B52",
            product_type: "drink",
            unit_name: "ly",
            quantity: 1,
            unit_price: 30000,
            line_total: 30000,
            note: ""
          }
        ]
      };
    }

    if (sql.includes("from invoice_payment_lines where")) {
      return {
        results: [
          { method: "cash", amount: 30000, created_at: "2026-05-20T08:30:00+07:00" }
        ]
      };
    }

    if (sql.includes("from inventory_daily_stock s")) {
      return {
        results: [
          {
            product_id: "BIA-HENIKEN",
            product_name: "BIA HENIKEN LON",
            unit_name: "Lon",
            opening_qty: 100,
            import_qty: 0,
            last_import_price: 18000,
            manual_export_qty: 0,
            sold_qty: 20,
            total_export_qty: 20,
            closing_qty: 80,
            min_stock: 5
          }
        ]
      };
    }

    throw new Error(`Unhandled all SQL: ${this.sql}`);
  }

  async run() {
    const sql = normalize(this.sql);
    if (sql.includes("insert into sync_nonces")) {
      const key = `${this.params[0]}|${this.params[1]}|${this.params[2]}`;
      if (this.db.nonces.has(key)) throw new Error("duplicate nonce");
      this.db.nonces.add(key);
      return { success: true };
    }

    if (sql.includes("insert into sync_logs")) {
      this.db.syncLogs.push({
        eventType: this.params[3],
        message: this.params[4],
        bodySha256: this.params[5],
        requestTimestamp: this.params[6] || "",
        requestNonce: this.params[7] || ""
      });
      return { success: true };
    }

    if (sql.includes("insert into inventory_daily_stock")) {
      this.db.inventoryRows.push({
        tenantId: this.params[0],
        storeId: this.params[1],
        businessDate: this.params[2],
        productId: this.params[3],
        productName: this.params[4],
        unitName: this.params[5],
        openingQty: this.params[6],
        importQty: this.params[7],
        lastImportPrice: this.params[8],
        manualExportQty: this.params[9],
        soldQty: this.params[10],
        totalExportQty: this.params[11],
        closingQty: this.params[12],
        minStock: this.params[13],
        updatedAt: this.params[14]
      });
      return { success: true };
    }

    if (sql.includes("insert into audit_logs")) {
      this.db.auditLogs.push({
        tenantId: this.params[1],
        storeId: this.params[2],
        actorType: this.params[3],
        action: this.params[4],
        message: this.params[5]
      });
      return { success: true };
    }

    if (sql.startsWith("delete from ")) {
      const table = sql.match(/^delete from ([a-z_]+)/)?.[1] || "";
      this.db.deletedTables.push(table);
      return { success: true, meta: { changes: 1 } };
    }

    if (sql.includes("update stores")) {
      return { success: true };
    }

    throw new Error(`Unhandled run SQL: ${this.sql}`);
  }
}

function invoiceRow() {
  return {
    tenant_id: "TENANT-001",
    store_id: "STORE-001",
    invoice_id: "INV-001",
    invoice_version: 2,
    status: "paid",
    business_date: "2026-05-20",
    table_name: "Ban A1",
    cashier: "Administrator",
    opened_at: "2026-05-20T08:00:00+07:00",
    paid_at: "2026-05-20T08:30:00+07:00",
    subtotal: 100000,
    discount: 10000,
    total: 90000,
    payment_method: "cash",
    discount_note: ""
  };
}

function normalize(sql) {
  return String(sql).replace(/\s+/g, " ").trim().toLowerCase();
}
