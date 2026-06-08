const H = {
  "content-type": "application/json; charset=utf-8",
  "access-control-allow-origin": "*",
  "access-control-allow-methods": "GET,POST,OPTIONS",
  "access-control-allow-headers": "authorization,content-type,x-bkpos-tenant,x-bkpos-store,x-bkpos-timestamp,x-bkpos-nonce,x-bkpos-signature",
  "cache-control": "no-store"
};
const SIG_WINDOW = 300;
const NONCE_TTL = 600;
const NONCE_CLEANUP_SECONDS = 86400;
const ACCESS_TTL = 3600;
const REFRESH_TTL = 2592000;
const PASSWORD_PBKDF2_ITERATIONS = 100000;
const PAY_METHODS = new Set(["cash", "transfer", "card", "other"]);
const INV_METHODS = new Set(["cash", "transfer", "card", "split", "other"]);
const PROD_TYPES = new Set(["food", "drink", "other"]);

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === "OPTIONS") return json({}, 204);
      if (request.method === "GET" && url.pathname === "/privacy") return privacyPolicy();

      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/provision") {
        requireAdmin(request, env);
        return json(await provision(env, await readJson(request)));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/sync-key") {
        requireAdmin(request, env);
        return json(await getActiveSyncKey(env, await readJson(request)));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/lookup-config") {
        requireAdmin(request, env);
        return json(await lookupRevenueCloudConfig(env, await readJson(request)));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/manager-user") {
        requireAdmin(request, env);
        return json(await upsertManagerUser(env, await readJson(request)));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/revoke") {
        requireAdmin(request, env);
        return json(await setRevenueCloudEnabled(env, await readJson(request), false));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/unrevoke") {
        requireAdmin(request, env);
        return json(await setRevenueCloudEnabled(env, await readJson(request), true));
      }
      if (request.method === "POST" && url.pathname === "/admin/revenue-cloud/delete-data") {
        requireAdmin(request, env);
        return json(await deleteRevenueCloudData(env, await readJson(request)));
      }

      if (request.method === "POST" && url.pathname === "/sync/heartbeat") {
        const { ctx, bytes } = await readSignedJson(request, env);
        await markHeartbeat(env, ctx, bytes);
        return json({ ok: true, tenantId: ctx.tenantId, storeId: ctx.storeId, time: now() });
      }

      if (request.method === "POST" && url.pathname === "/sync/invoices/batch") {
        const { ctx, body } = await readSignedJson(request, env);
        return json(await syncInvoices(env, ctx, Array.isArray(body.invoices) ? body.invoices : []));
      }

      if (request.method === "POST" && url.pathname === "/sync/open-tables/batch") {
        const { ctx, body } = await readSignedJson(request, env);
        return json(await syncOpenTables(env, ctx, Array.isArray(body.tables) ? body.tables : []));
      }

      if (request.method === "POST" && url.pathname === "/sync/inventory-daily") {
        const { ctx, body } = await readSignedJson(request, env);
        return json(await syncInventoryDaily(env, ctx, body));
      }

      const syncOne = url.pathname.match(/^\/sync\/invoices\/([^/]+)$/);
      if (request.method === "POST" && syncOne) {
        const { ctx, body } = await readSignedJson(request, env);
        return json(await syncInvoices(env, ctx, [{ ...body, invoiceId: body.invoiceId || decodeURIComponent(syncOne[1]) }]));
      }

      if (request.method === "GET" && url.pathname === "/sync/status") {
        const ctx = await requireSigned(request, env, new Uint8Array());
        const store = await getStore(env, ctx.tenantId, ctx.storeId);
        return json({ ok: true, tenantId: ctx.tenantId, storeId: ctx.storeId, lastHeartbeatAt: store?.last_heartbeat_at || null, lastSyncAt: store?.last_sync_at || null, lastError: store?.last_error || null });
      }

      if (request.method === "POST" && url.pathname === "/auth/login") return json(await login(env, await readJson(request)));
      if (request.method === "POST" && url.pathname === "/auth/refresh") return json(await refresh(env, await readJson(request)));
      if (request.method === "POST" && url.pathname === "/auth/logout") {
        const s = await manager(request, env, true);
        await revoke(env, s.session_id);
        return json({ ok: true });
      }

      if (request.method === "GET" && url.pathname === "/stores") {
        const s = await manager(request, env);
        return json(await stores(env, s.tenant_id));
      }

      if (request.method === "GET" && url.pathname === "/reports/today") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        const requested = dateOnly(url.searchParams.get("date"));
        const d = requested || utcDate(new Date());
        return json(await reportToday(env, store, d, requested ? "request" : "utc_fallback"));
      }

      if (request.method === "GET" && url.pathname === "/reports/range") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        const from = reqDate(url.searchParams.get("from"), "from");
        const to = reqDate(url.searchParams.get("to"), "to");
        return json(await reportRange(env, store, from, to));
      }

      if (request.method === "GET" && url.pathname === "/reports/month") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        const month = reqMonth(url.searchParams.get("month"));
        return json(await reportMonth(env, store, month));
      }

      if (request.method === "GET" && url.pathname === "/reports/top-products") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        return json(await topProducts(env, store, reqDate(url.searchParams.get("from"), "from"), reqDate(url.searchParams.get("to"), "to"), clamp(url.searchParams.get("limit"), 1, 100, 20)));
      }

      if (request.method === "GET" && url.pathname === "/reports/open-tables") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        return json(await reportOpenTables(env, store));
      }

      if (request.method === "GET" && url.pathname === "/reports/inventory") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        return json(await reportInventory(env, store, url.searchParams));
      }

      if (request.method === "GET" && url.pathname === "/invoices") {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        return json(await invoiceList(env, store, url.searchParams));
      }

      const inv = url.pathname.match(/^\/invoices\/([^/]+)$/);
      if (request.method === "GET" && inv) {
        const s = await manager(request, env);
        const store = await reportStore(env, s.tenant_id, url.searchParams.get("storeId"));
        return json(await invoiceDetail(env, store, decodeURIComponent(inv[1])));
      }

      return json({ error: "not_found", message: "Endpoint not found." }, 404);
    } catch (e) {
      if (e instanceof Response) return e;
      return json({ error: "server_error", message: String(e?.message || e) }, 500);
    }
  },
  async scheduled(_event, env) { await cleanupNonces(env); }
};
async function provision(env, b) {
  const tenantId = id(b.tenantId || `TENANT-${crypto.randomUUID()}`);
  const storeId = id(b.storeId || `STORE-${crypto.randomUUID()}`);
  const keyId = id(b.keyId || `KEY-${crypto.randomUUID()}`);
  const syncKey = text(b.syncKey) || token(48);
  const tz = text(b.timezone || env.DEFAULT_TIMEZONE || "Asia/Ho_Chi_Minh") || "Asia/Ho_Chi_Minh";
  await env.DB.batch([
    env.DB.prepare("INSERT INTO tenants (tenant_id,name,enabled,updated_at) VALUES (?,?,1,?) ON CONFLICT(tenant_id) DO UPDATE SET name=excluded.name,enabled=1,updated_at=excluded.updated_at").bind(tenantId, text(b.tenantName || b.customerName || tenantId), now()),
    env.DB.prepare("INSERT INTO stores (tenant_id,store_id,name,timezone,revenue_cloud_enabled,updated_at) VALUES (?,?,?,?,1,?) ON CONFLICT(tenant_id,store_id) DO UPDATE SET name=excluded.name,timezone=excluded.timezone,revenue_cloud_enabled=1,updated_at=excluded.updated_at").bind(tenantId, storeId, text(b.storeName || storeId), tz, now()),
    env.DB.prepare("UPDATE store_sync_keys SET is_active=0,revoked_at=? WHERE tenant_id=? AND store_id=? AND is_active=1").bind(now(), tenantId, storeId),
    env.DB.prepare("INSERT INTO store_sync_keys (tenant_id,store_id,key_id,sync_key,is_active) VALUES (?,?,?,?,1)").bind(tenantId, storeId, keyId, syncKey)
  ]);
  if (b.managerUsername && b.managerPassword) await upsertUser(env, tenantId, b.managerUsername, b.managerPassword, b.managerDisplayName || b.managerUsername);
  await audit(env, tenantId, storeId, "admin", "provision_revenue_cloud", "Revenue Cloud provisioned.");
  return { ok: true, tenantId, storeId, keyId, syncKey };
}

async function getActiveSyncKey(env, b) {
  const tenantId = text(b.tenantId);
  const storeId = text(b.storeId || "MAIN");
  if (!tenantId || !storeId) bad("invalid_tenant_store");
  const store = await getStore(env, tenantId, storeId);
  if (!store) notFound("store_not_found");
  const key = await env.DB.prepare("SELECT key_id,sync_key,created_at FROM store_sync_keys WHERE tenant_id=? AND store_id=? AND is_active=1 ORDER BY created_at DESC LIMIT 1")
    .bind(tenantId, storeId).first();
  if (!key?.sync_key) notFound("sync_key_not_found");
  await audit(env, tenantId, storeId, "admin", "read_sync_key", "Active Revenue Cloud sync key retrieved by KeyGen.");
  return {
    ok: true,
    tenantId,
    storeId,
    keyId: key.key_id || "",
    syncKey: key.sync_key,
    revenueCloudEnabled: store.revenue_cloud_enabled === 1,
    timezone: store.timezone || "Asia/Ho_Chi_Minh",
    createdAt: key.created_at || null
  };
}

async function lookupRevenueCloudConfig(env, b) {
  const licenseId = id(b.licenseId || b.LicenseId || b.tenantId || b.TenantId);
  const tenantId = id(b.tenantId || b.TenantId || licenseId);
  const storeId = id(b.storeId || b.StoreId || "MAIN");
  const customerName = text(b.customerName || b.CustomerName);
  const phone = digits(b.phone || b.Phone);
  const candidates = new Map();

  const add = row => {
    if (!row?.sync_key) return;
    candidates.set(`${row.tenant_id}|${row.store_id}`, row);
  };

  if (tenantId && storeId) {
    add(await readActiveSyncKeyRow(env, tenantId, storeId));
  }

  if (licenseId) {
    const rows = await env.DB.prepare(`SELECT s.tenant_id,s.store_id,s.name AS store_name,s.timezone,s.revenue_cloud_enabled,t.name AS tenant_name,k.key_id,k.sync_key,k.created_at
      FROM stores s
      JOIN tenants t ON t.tenant_id=s.tenant_id
      JOIN store_sync_keys k ON k.tenant_id=s.tenant_id AND k.store_id=s.store_id AND k.is_active=1
      WHERE s.tenant_id=?
      ORDER BY CASE WHEN s.store_id='MAIN' THEN 0 ELSE 1 END,s.store_id,k.created_at DESC`)
      .bind(licenseId).all();
    for (const row of rows.results || []) add(row);
  }

  if (phone) {
    const rows = await env.DB.prepare(`SELECT s.tenant_id,s.store_id,s.name AS store_name,s.timezone,s.revenue_cloud_enabled,t.name AS tenant_name,k.key_id,k.sync_key,k.created_at
      FROM manager_users u
      JOIN stores s ON s.tenant_id=u.tenant_id
      JOIN tenants t ON t.tenant_id=s.tenant_id
      JOIN store_sync_keys k ON k.tenant_id=s.tenant_id AND k.store_id=s.store_id AND k.is_active=1
      WHERE u.username=? AND u.is_active=1
      ORDER BY CASE WHEN s.store_id='MAIN' THEN 0 ELSE 1 END,s.store_id,k.created_at DESC`)
      .bind(phone).all();
    for (const row of rows.results || []) add(row);
  }

  if (customerName) {
    const likeName = `%${customerName}%`;
    const rows = await env.DB.prepare(`SELECT s.tenant_id,s.store_id,s.name AS store_name,s.timezone,s.revenue_cloud_enabled,t.name AS tenant_name,k.key_id,k.sync_key,k.created_at
      FROM stores s
      JOIN tenants t ON t.tenant_id=s.tenant_id
      JOIN store_sync_keys k ON k.tenant_id=s.tenant_id AND k.store_id=s.store_id AND k.is_active=1
      WHERE lower(t.name)=lower(?) OR lower(s.name)=lower(?) OR lower(t.name) LIKE lower(?) OR lower(s.name) LIKE lower(?)
      ORDER BY CASE WHEN s.store_id='MAIN' THEN 0 ELSE 1 END,s.store_id,k.created_at DESC`)
      .bind(customerName, customerName, likeName, likeName).all();
    for (const row of rows.results || []) add(row);
  }

  const matches = [...candidates.values()];
  if (matches.length === 0) notFound("revenue_cloud_config_not_found");

  const preferred = matches.find(row => row.tenant_id === licenseId && row.store_id === "MAIN")
    || matches.find(row => row.store_id === "MAIN");
  const selected = preferred || (matches.length === 1 ? matches[0] : null);
  if (!selected) {
    conflict("ambiguous_revenue_cloud_config", {
      candidates: matches.map(row => ({
        tenantId: row.tenant_id,
        storeId: row.store_id,
        tenantName: row.tenant_name || "",
        storeName: row.store_name || ""
      }))
    });
  }

  await audit(env, selected.tenant_id, selected.store_id, "admin", "lookup_sync_key", "Revenue Cloud sync config looked up by KeyGen.");
  return toSyncConfigResult(selected);
}

async function readActiveSyncKeyRow(env, tenantId, storeId) {
  return await env.DB.prepare(`SELECT s.tenant_id,s.store_id,s.name AS store_name,s.timezone,s.revenue_cloud_enabled,t.name AS tenant_name,k.key_id,k.sync_key,k.created_at
    FROM stores s
    JOIN tenants t ON t.tenant_id=s.tenant_id
    JOIN store_sync_keys k ON k.tenant_id=s.tenant_id AND k.store_id=s.store_id AND k.is_active=1
    WHERE s.tenant_id=? AND s.store_id=?
    ORDER BY k.created_at DESC
    LIMIT 1`)
    .bind(tenantId, storeId).first();
}

function toSyncConfigResult(row) {
  return {
    ok: true,
    tenantId: row.tenant_id,
    storeId: row.store_id,
    keyId: row.key_id || "",
    syncKey: row.sync_key,
    revenueCloudEnabled: row.revenue_cloud_enabled === 1,
    timezone: row.timezone || "Asia/Ho_Chi_Minh",
    tenantName: row.tenant_name || "",
    storeName: row.store_name || "",
    createdAt: row.created_at || null
  };
}

async function setRevenueCloudEnabled(env, b, enabled) {
  const tenantId = text(b.tenantId), storeId = text(b.storeId);
  if (!tenantId || !storeId) bad("invalid_tenant_store");
  const store = await getStore(env, tenantId, storeId);
  if (!store) notFound("store_not_found");
  await env.DB.batch([
    env.DB.prepare("UPDATE stores SET revenue_cloud_enabled=?,last_error=?,updated_at=? WHERE tenant_id=? AND store_id=?")
      .bind(enabled ? 1 : 0, enabled ? null : "revenue_cloud_revoked", now(), tenantId, storeId),
    env.DB.prepare("UPDATE manager_sessions SET revoked_at=? WHERE tenant_id=? AND revoked_at IS NULL")
      .bind(now(), tenantId)
  ]);
  await audit(env, tenantId, storeId, "admin", enabled ? "unrevoke_revenue_cloud" : "revoke_revenue_cloud", enabled ? "Revenue Cloud enabled." : "Revenue Cloud revoked.");
  return { ok: true, tenantId, storeId, revenueCloudEnabled: enabled };
}

async function deleteRevenueCloudData(env, b) {
  const tenantId = text(b.tenantId);
  const storeId = text(b.storeId);
  const confirm = text(b.confirm);
  if (!tenantId || !storeId) bad("invalid_tenant_store");
  if (confirm !== "DELETE_REVENUE_CLOUD_DATA") bad("confirmation_required");

  const store = await getStore(env, tenantId, storeId);
  if (!store) notFound("store_not_found");

  const counts = {};
  counts.invoicePaymentLines = await deleteRows(env.DB.prepare("DELETE FROM invoice_payment_lines WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.invoiceItems = await deleteRows(env.DB.prepare("DELETE FROM invoice_items WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.invoices = await deleteRows(env.DB.prepare("DELETE FROM invoices WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.openTableItems = await deleteRows(env.DB.prepare("DELETE FROM open_table_items WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.openTables = await deleteRows(env.DB.prepare("DELETE FROM open_tables WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.inventoryDailyStock = await deleteRows(env.DB.prepare("DELETE FROM inventory_daily_stock WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.dailyRevenueSnapshots = await deleteRows(env.DB.prepare("DELETE FROM daily_revenue_snapshots WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.syncNonces = await deleteRows(env.DB.prepare("DELETE FROM sync_nonces WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));
  counts.syncLogs = await deleteRows(env.DB.prepare("DELETE FROM sync_logs WHERE tenant_id=? AND store_id=?").bind(tenantId, storeId));

  const deletedAt = now();
  await env.DB.prepare("UPDATE stores SET last_sync_at=NULL,last_error=NULL,updated_at=? WHERE tenant_id=? AND store_id=?")
    .bind(deletedAt, tenantId, storeId)
    .run();

  const totalDeleted = Object.values(counts).reduce((sum, value) => sum + Number(value || 0), 0);
  await audit(env, tenantId, storeId, "admin", "delete_revenue_cloud_data", `Revenue Cloud report data deleted. rows=${totalDeleted}`);
  return { ok: true, tenantId, storeId, deletedAt, counts, totalDeleted };
}

async function deleteRows(statement) {
  const result = await statement.run();
  return Number(result?.meta?.changes || result?.changes || 0);
}

async function upsertManagerUser(env, b) {
  const tenantId = text(b.tenantId);
  const storeId = text(b.storeId);
  const username = text(b.managerUsername || b.username);
  const password = String(b.managerPassword ?? b.password ?? "");
  const displayName = text(b.managerDisplayName || b.displayName || username);
  if (!tenantId || !username || !password) bad("invalid_manager_user");

  const tenant = await env.DB.prepare("SELECT tenant_id FROM tenants WHERE tenant_id=? AND enabled=1").bind(tenantId).first();
  if (!tenant) notFound("tenant_not_found");

  if (storeId) {
    const store = await getStore(env, tenantId, storeId);
    if (!store) notFound("store_not_found");
  }

  await upsertUser(env, tenantId, username, password, displayName);
  await audit(env, tenantId, storeId, "admin", "upsert_manager_user", `Manager user ${username.toLowerCase()} upserted.`);
  return { ok: true, tenantId, storeId, username: username.toLowerCase(), displayName };
}

async function upsertUser(env, tenantId, username, password, displayName) {
  const u = text(username).toLowerCase();
  const salt = token(18);
  const hash = await hashPassword(password, salt);
  const old = await env.DB.prepare("SELECT user_id FROM manager_users WHERE tenant_id=? AND username=?").bind(tenantId, u).first();
  await env.DB.prepare(`INSERT INTO manager_users (user_id,tenant_id,username,display_name,password_hash,password_salt,is_active,updated_at)
    VALUES (?,?,?,?,?,?,1,?) ON CONFLICT(tenant_id,username) DO UPDATE SET display_name=excluded.display_name,password_hash=excluded.password_hash,password_salt=excluded.password_salt,is_active=1,updated_at=excluded.updated_at`)
    .bind(old?.user_id || `USER-${crypto.randomUUID()}`, tenantId, u, text(displayName), hash, salt, now()).run();
}

async function readSignedJson(request, env) {
  const bytes = new Uint8Array(await request.arrayBuffer());
  const ctx = await requireSigned(request, env, bytes);
  return { ctx, bytes, body: parseJson(bytes) };
}

async function requireSigned(request, env, bytes) {
  const tenantId = text(request.headers.get("X-BKPOS-Tenant"));
  const storeId = text(request.headers.get("X-BKPOS-Store"));
  const timestamp = text(request.headers.get("X-BKPOS-Timestamp"));
  const nonce = text(request.headers.get("X-BKPOS-Nonce")).toLowerCase();
  const sig = text(request.headers.get("X-BKPOS-Signature")).replace(/^sha256=/i, "").toLowerCase();
  if (!tenantId || !storeId || !timestamp || !nonce || !/^[0-9a-f]{64}$/.test(sig)) unauth("invalid_signature_headers");
  const ts = Number(timestamp);
  if (!Number.isFinite(ts) || Math.abs(Math.floor(Date.now() / 1000) - ts) > SIG_WINDOW) unauth("timestamp_out_of_window");
  const store = await getStore(env, tenantId, storeId);
  if (!store || store.tenant_enabled !== 1 || store.revenue_cloud_enabled !== 1) unauth("store_not_enabled");
  const keys = await env.DB.prepare("SELECT sync_key FROM store_sync_keys WHERE tenant_id=? AND store_id=? AND is_active=1").bind(tenantId, storeId).all();
  let ok = false;
  for (const k of keys.results || []) if (ctEqual(await hmac(k.sync_key, timestamp, nonce, bytes), sig)) ok = true;
  if (!ok) {
    await logSync(env, tenantId, storeId, "invalid_signature", `ts=${timestamp};nonce=${nonce}`, await sha256(bytes), timestamp, nonce);
    unauth("invalid_signature");
  }
  await cleanupNoncesMaybe(env);
  try {
    await env.DB.prepare("INSERT INTO sync_nonces (tenant_id,store_id,nonce,created_at,expires_at) VALUES (?,?,?,?,?)")
      .bind(tenantId, storeId, nonce, now(), new Date(Date.now() + NONCE_TTL * 1000).toISOString()).run();
  } catch { unauth("replay_detected"); }
  return { tenantId, storeId, timezone: store.timezone || "Asia/Ho_Chi_Minh", timestamp, nonce };
}

async function getStore(env, tenantId, storeId) {
  return await env.DB.prepare(`SELECT s.*,t.enabled AS tenant_enabled FROM stores s JOIN tenants t ON t.tenant_id=s.tenant_id WHERE s.tenant_id=? AND s.store_id=?`).bind(tenantId, storeId).first();
}

async function markHeartbeat(env, ctx, bytes) {
  await env.DB.batch([
    env.DB.prepare("UPDATE stores SET last_heartbeat_at=?,last_error=NULL,updated_at=? WHERE tenant_id=? AND store_id=?").bind(now(), now(), ctx.tenantId, ctx.storeId),
    env.DB.prepare("INSERT INTO sync_logs (log_id,tenant_id,store_id,event_type,message,body_sha256) VALUES (?,?,?,?,?,?)").bind(crypto.randomUUID(), ctx.tenantId, ctx.storeId, "heartbeat", "Heartbeat accepted.", await sha256(bytes))
  ]);
}

async function syncInvoices(env, ctx, invoices) {
  const dates = new Set(); let accepted = 0, ignored = 0, failed = 0; const errors = [];
  for (const raw of invoices) {
    try { (await upsertInvoice(env, ctx, raw, dates)) === "accepted" ? accepted++ : ignored++; }
    catch (e) { failed++; errors.push({ invoiceId: raw?.invoiceId || "", error: String(e?.message || e) }); }
  }
  for (const d of dates) await recomputeDay(env, ctx.tenantId, ctx.storeId, d);
  await env.DB.prepare("UPDATE stores SET last_sync_at=?,last_error=?,updated_at=? WHERE tenant_id=? AND store_id=?").bind(now(), failed ? `Failed ${failed} invoice(s).` : null, now(), ctx.tenantId, ctx.storeId).run();
  await logSync(env, ctx.tenantId, ctx.storeId, "invoice_batch", `accepted=${accepted};ignored=${ignored};failed=${failed}`, "");
  return { ok: failed === 0, accepted, ignored, failed, errors, affectedDates: [...dates] };
}

async function syncOpenTables(env, ctx, tables) {
  const syncAt = now();
  let accepted = 0, failed = 0; const errors = [];
  for (const raw of tables) {
    try {
      const x = normOpenTable(raw);
      await env.DB.prepare(`INSERT INTO open_tables (tenant_id,store_id,table_id,table_name,zone_id,zone_name,order_id,occupied_at,total,modified_at,active,synced_at)
        VALUES (?,?,?,?,?,?,?,?,?,?,1,?)
        ON CONFLICT(tenant_id,store_id,table_id) DO UPDATE SET table_name=excluded.table_name,zone_id=excluded.zone_id,zone_name=excluded.zone_name,order_id=excluded.order_id,occupied_at=excluded.occupied_at,total=excluded.total,modified_at=excluded.modified_at,active=1,synced_at=excluded.synced_at`)
        .bind(ctx.tenantId, ctx.storeId, x.tableId, x.tableName, x.zoneId, x.zoneName, x.orderId, x.occupiedAt, x.total, x.modifiedAt, syncAt).run();
      await env.DB.prepare("DELETE FROM open_table_items WHERE tenant_id=? AND store_id=? AND table_id=?")
        .bind(ctx.tenantId, ctx.storeId, x.tableId).run();
      for (const it of x.items) {
        await env.DB.prepare(`INSERT INTO open_table_items (tenant_id,store_id,table_id,order_id,line_id,product_id,product_name,product_type,unit_name,quantity,unit_price,line_total,note,synced_at)
          VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)`)
          .bind(ctx.tenantId, ctx.storeId, x.tableId, x.orderId, it.lineId, it.productId, it.productName, it.productType, it.unitName, it.quantity, it.unitPrice, it.lineTotal, it.note, syncAt).run();
      }
      accepted++;
    } catch (e) {
      failed++;
      errors.push({ tableId: text(raw?.tableId), error: String(e?.message || e) });
    }
  }

  if (failed === 0) {
    await env.DB.prepare("UPDATE open_tables SET active=0,synced_at=? WHERE tenant_id=? AND store_id=? AND active=1 AND synced_at<>?")
      .bind(syncAt, ctx.tenantId, ctx.storeId, syncAt).run();
  }

  await env.DB.prepare("UPDATE stores SET last_sync_at=?,last_error=?,updated_at=? WHERE tenant_id=? AND store_id=?")
    .bind(syncAt, failed ? `Failed ${failed} open table(s).` : null, syncAt, ctx.tenantId, ctx.storeId).run();
  await logSync(env, ctx.tenantId, ctx.storeId, "open_tables_batch", `accepted=${accepted};failed=${failed}`, "");
  return { ok: failed === 0, accepted, failed, errors, syncedAt: syncAt };
}

async function syncInventoryDaily(env, ctx, body) {
  if (!body || typeof body !== "object") bad("invalid_inventory_payload");
  const businessDate = dateOnly(body.businessDate);
  if (!businessDate) bad("invalid_business_date");
  const rows = Array.isArray(body.items) ? body.items : [];
  const syncAt = now();
  let accepted = 0, failed = 0;
  const errors = [];

  for (const raw of rows) {
    try {
      const x = normInventoryItem(raw);
      await env.DB.prepare(`INSERT INTO inventory_daily_stock (tenant_id,store_id,business_date,product_id,product_name,unit_name,opening_qty,import_qty,last_import_price,manual_export_qty,sold_qty,total_export_qty,closing_qty,min_stock,updated_at)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        ON CONFLICT(tenant_id,store_id,business_date,product_id) DO UPDATE SET product_name=excluded.product_name,unit_name=excluded.unit_name,opening_qty=excluded.opening_qty,import_qty=excluded.import_qty,last_import_price=excluded.last_import_price,manual_export_qty=excluded.manual_export_qty,sold_qty=excluded.sold_qty,total_export_qty=excluded.total_export_qty,closing_qty=excluded.closing_qty,min_stock=excluded.min_stock,updated_at=excluded.updated_at`)
        .bind(ctx.tenantId, ctx.storeId, businessDate, x.productId, x.productName, x.unitName, x.openingQty, x.importQty, x.lastImportPrice, x.manualExportQty, x.soldQty, x.totalExportQty, x.closingQty, x.minStock, syncAt)
        .run();
      accepted++;
    } catch (e) {
      failed++;
      errors.push({ productId: text(raw?.productId), error: String(e?.message || e) });
    }
  }

  await env.DB.prepare("UPDATE stores SET last_sync_at=?,last_error=?,updated_at=? WHERE tenant_id=? AND store_id=?")
    .bind(syncAt, failed ? `Failed ${failed} inventory item(s).` : null, syncAt, ctx.tenantId, ctx.storeId).run();
  await logSync(env, ctx.tenantId, ctx.storeId, "inventory_daily", `businessDate=${businessDate};accepted=${accepted};failed=${failed}`, "");
  return { ok: failed === 0, businessDate, accepted, failed, errors, syncedAt: syncAt };
}

function normInventoryItem(raw) {
  if (!raw || typeof raw !== "object") throw new Error("invalid_inventory_item");
  const productId = text(raw.productId);
  if (!productId) throw new Error("missing_product_id");
  const manualExportQty = qty(raw.manualExportQty);
  const soldQty = qty(raw.soldQty);
  const totalExportQty = raw.totalExportQty === undefined || raw.totalExportQty === null
    ? manualExportQty + soldQty
    : qty(raw.totalExportQty);
  return {
    productId,
    productName: text(raw.productName) || productId,
    unitName: text(raw.unitName),
    openingQty: qty(raw.openingQty, true),
    importQty: qty(raw.importQty),
    lastImportPrice: money(raw.lastImportPrice),
    manualExportQty,
    soldQty,
    totalExportQty,
    closingQty: qty(raw.closingQty, true),
    minStock: qty(raw.minStock)
  };
}

async function upsertInvoice(env, ctx, raw, dates) {
  const x = normInvoice(ctx, raw);
  const old = await env.DB.prepare("SELECT invoice_version,business_date FROM invoices WHERE tenant_id=? AND store_id=? AND invoice_id=?").bind(ctx.tenantId, ctx.storeId, x.invoiceId).first();
  if (old && Number(old.invoice_version) > x.invoiceVersion) return "ignored_stale";
  if (old?.business_date) dates.add(old.business_date); dates.add(x.businessDate);
  await env.DB.batch([
    env.DB.prepare(`INSERT INTO invoices (tenant_id,store_id,invoice_id,invoice_version,status,table_name,cashier,opened_at,paid_at,business_date,subtotal,discount,total,payment_method,discount_note,modified_at,synced_at,raw_payload)
      VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(tenant_id,store_id,invoice_id) DO UPDATE SET invoice_version=excluded.invoice_version,status=excluded.status,table_name=excluded.table_name,cashier=excluded.cashier,opened_at=excluded.opened_at,paid_at=excluded.paid_at,business_date=excluded.business_date,subtotal=excluded.subtotal,discount=excluded.discount,total=excluded.total,payment_method=excluded.payment_method,discount_note=excluded.discount_note,modified_at=excluded.modified_at,synced_at=excluded.synced_at,raw_payload=excluded.raw_payload`)
      .bind(ctx.tenantId, ctx.storeId, x.invoiceId, x.invoiceVersion, x.status, x.tableName, x.cashier, x.openedAt, x.paidAt, x.businessDate, x.subtotal, x.discount, x.total, x.paymentMethod, x.discountNote, x.modifiedAt, now(), JSON.stringify(raw || {})),
    env.DB.prepare("DELETE FROM invoice_items WHERE tenant_id=? AND store_id=? AND invoice_id=?").bind(ctx.tenantId, ctx.storeId, x.invoiceId),
    env.DB.prepare("DELETE FROM invoice_payment_lines WHERE tenant_id=? AND store_id=? AND invoice_id=?").bind(ctx.tenantId, ctx.storeId, x.invoiceId)
  ]);
  for (const it of x.items) await env.DB.prepare("INSERT INTO invoice_items (tenant_id,store_id,invoice_id,line_id,product_id,product_name,product_type,unit_name,quantity,unit_price,line_total,note) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)").bind(ctx.tenantId, ctx.storeId, x.invoiceId, it.lineId, it.productId, it.productName, it.productType, it.unitName, it.quantity, it.unitPrice, it.lineTotal, it.note).run();
  for (const p of x.payments) await env.DB.prepare("INSERT INTO invoice_payment_lines (tenant_id,store_id,invoice_id,payment_id,method,amount,created_at) VALUES (?,?,?,?,?,?,?)").bind(ctx.tenantId, ctx.storeId, x.invoiceId, p.paymentId, p.method, p.amount, p.createdAt).run();
  return "accepted";
}
function normInvoice(ctx, raw) {
  if (!raw || typeof raw !== "object") throw new Error("invalid_invoice_payload");
  const invoiceId = text(raw.invoiceId); if (!invoiceId) throw new Error("missing_invoice_id");
  const invoiceVersion = Math.max(1, Math.trunc(Number(raw.invoiceVersion || 1)));
  const status = invStatus(raw.status) || "paid";
  const paidAt = iso(raw.paidAt), openedAt = iso(raw.openedAt), modifiedAt = iso(raw.modifiedAt);
  const businessDate = dateOnly(raw.businessDate) || dateFrom(paidAt || openedAt || modifiedAt) || utcDate(new Date());
  const subtotal = money(raw.subtotal), discount = money(raw.discount), total = money(raw.total || Math.max(0, subtotal - discount));
  const payments = normPayments(Array.isArray(raw.payments) ? raw.payments : [], raw.paymentMethod, total, paidAt || modifiedAt || openedAt);
  return { invoiceId, invoiceVersion, status, tableName: text(raw.tableName), cashier: text(raw.cashier), openedAt, paidAt, businessDate, subtotal, discount, total, paymentMethod: invMethod(raw.paymentMethod, payments), discountNote: text(raw.discountNote), modifiedAt, payments, items: normItems(raw.items) };
}
function normPayments(rows, fallback, total, createdAt) {
  const out = [];
  for (let i = 0; i < rows.length; i++) {
    const amount = money(rows[i]?.amount); if (amount <= 0) continue;
    const method = payMethod(rows[i]?.method);
    out.push({ paymentId: text(rows[i]?.paymentId) || `${i + 1}-${method}`, method, amount, createdAt: iso(rows[i]?.createdAt) || createdAt || now() });
  }
  if (!out.length && total > 0) out.push({ paymentId: "fallback-1", method: payMethod(fallback), amount: total, createdAt: createdAt || now() });
  return out;
}
function normItems(items) {
  if (!Array.isArray(items)) return [];
  return items.map((it, i) => {
    const q = Number(it?.quantity || 0), up = money(it?.unitPrice), lt = money(it?.lineTotal || q * up);
    return { lineId: text(it?.lineId) || `line-${i + 1}`, productId: text(it?.productId), productName: text(it?.productName), productType: prodType(it?.productType), unitName: text(it?.unitName), quantity: q, unitPrice: up, lineTotal: lt, note: text(it?.note) };
  });
}

function normOpenTable(raw) {
  if (!raw || typeof raw !== "object") throw new Error("invalid_table_payload");
  const tableId = text(raw.tableId); if (!tableId) throw new Error("missing_table_id");
  const orderId = text(raw.orderId); if (!orderId) throw new Error("missing_order_id");
  return {
    tableId,
    tableName: text(raw.tableName) || tableId,
    zoneId: text(raw.zoneId),
    zoneName: text(raw.zoneName),
    orderId,
    occupiedAt: iso(raw.occupiedAt),
    total: money(raw.total),
    modifiedAt: iso(raw.modifiedAt) || now(),
    items: normItems(raw.items)
  };
}

async function recomputeDay(env, tenantId, storeId, d) {
  const s = await env.DB.prepare("SELECT COALESCE(SUM(CASE WHEN status IN ('paid','edited') THEN total ELSE 0 END),0) revenue,COALESCE(SUM(CASE WHEN status IN ('paid','edited') THEN 1 ELSE 0 END),0) invoice_count,COALESCE(SUM(CASE WHEN status='cancelled' THEN 1 ELSE 0 END),0) cancelled_invoice_count FROM invoices WHERE tenant_id=? AND store_id=? AND business_date=?").bind(tenantId, storeId, d).first();
  const p = await payBreak(env, tenantId, storeId, d, d);
  const count = Number(s?.invoice_count || 0), rev = Number(s?.revenue || 0);
  await env.DB.prepare(`INSERT INTO daily_revenue_snapshots (tenant_id,store_id,business_date,revenue,invoice_count,cancelled_invoice_count,average_invoice_value,cash_amount,transfer_amount,card_amount,other_amount,updated_at)
    VALUES (?,?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(tenant_id,store_id,business_date) DO UPDATE SET revenue=excluded.revenue,invoice_count=excluded.invoice_count,cancelled_invoice_count=excluded.cancelled_invoice_count,average_invoice_value=excluded.average_invoice_value,cash_amount=excluded.cash_amount,transfer_amount=excluded.transfer_amount,card_amount=excluded.card_amount,other_amount=excluded.other_amount,updated_at=excluded.updated_at`)
    .bind(tenantId, storeId, d, rev, count, Number(s?.cancelled_invoice_count || 0), count ? Math.round(rev / count) : 0, p.cash, p.transfer, p.card, p.other, now()).run();
}

async function reportToday(env, store, d, source) {
  return { storeId: store.store_id, timezone: store.timezone, dateSource: source, businessDate: d, lastSyncAt: store.last_sync_at || null, summary: await summary(env, store, d, d), revenue7Days: await daily(env, store, addDays(d, -6), d), paymentBreakdown: await payBreakArray(env, store, d, d) };
}
async function reportRange(env, store, from, to) { return { from, to, summary: await summary(env, store, from, to), daily: await daily(env, store, from, to), paymentBreakdown: await payBreakArray(env, store, from, to) }; }
async function reportMonth(env, store, month) { const from = `${month}-01`, to = addDays(nextMonth(month), -1); return { month, timezone: store.timezone, summary: await summary(env, store, from, to), daily: await daily(env, store, from, to), paymentBreakdown: await payBreakArray(env, store, from, to) }; }
async function reportOpenTables(env, store) {
  const r = await env.DB.prepare("SELECT table_id,table_name,zone_id,zone_name,order_id,occupied_at,total,modified_at,synced_at FROM open_tables WHERE tenant_id=? AND store_id=? AND active=1 ORDER BY occupied_at,table_name")
    .bind(store.tenant_id, store.store_id).all();
  const itemRows = await env.DB.prepare(`SELECT oi.table_id,oi.order_id,oi.line_id,oi.product_id,oi.product_name,oi.product_type,oi.unit_name,oi.quantity,oi.unit_price,oi.line_total,oi.note
    FROM open_table_items oi
    JOIN open_tables ot ON ot.tenant_id=oi.tenant_id AND ot.store_id=oi.store_id AND ot.table_id=oi.table_id
    WHERE oi.tenant_id=? AND oi.store_id=? AND ot.active=1
    ORDER BY oi.table_id,oi.line_id`)
    .bind(store.tenant_id, store.store_id).all();
  const itemsByTable = new Map();
  for (const x of itemRows.results || []) {
    const key = x.table_id || "";
    if (!itemsByTable.has(key)) itemsByTable.set(key, []);
    itemsByTable.get(key).push({
      lineId: x.line_id || "",
      productId: x.product_id || "",
      productName: x.product_name || "",
      productType: prodType(x.product_type),
      unitName: x.unit_name || "",
      quantity: Number(x.quantity || 0),
      unitPrice: Number(x.unit_price || 0),
      lineTotal: Number(x.line_total || 0),
      note: x.note || ""
    });
  }
  const rows = (r.results || []).map(x => ({
    tableId: x.table_id || "",
    tableName: x.table_name || "",
    zoneId: x.zone_id || "",
    zoneName: x.zone_name || "",
    orderId: x.order_id || "",
    occupiedAt: x.occupied_at || null,
    total: Number(x.total || 0),
    modifiedAt: x.modified_at || null,
    syncedAt: x.synced_at || null,
    items: itemsByTable.get(x.table_id || "") || []
  }));
  return {
    storeId: store.store_id,
    lastSyncAt: store.last_sync_at || null,
    tableCount: rows.length,
    estimatedTotal: rows.reduce((sum, row) => sum + Number(row.total || 0), 0),
    tables: rows
  };
}
async function summary(env, store, from, to) {
  const r = await env.DB.prepare("SELECT COALESCE(SUM(CASE WHEN status IN ('paid','edited') THEN total ELSE 0 END),0) revenue,COALESCE(SUM(CASE WHEN status IN ('paid','edited') THEN 1 ELSE 0 END),0) invoice_count,COALESCE(SUM(CASE WHEN status='cancelled' THEN 1 ELSE 0 END),0) cancelled_invoice_count FROM invoices WHERE tenant_id=? AND store_id=? AND business_date>=? AND business_date<=?").bind(store.tenant_id, store.store_id, from, to).first();
  const p = await payBreak(env, store.tenant_id, store.store_id, from, to); const count = Number(r?.invoice_count || 0), rev = Number(r?.revenue || 0);
  return { revenue: rev, invoiceCount: count, cancelledInvoiceCount: Number(r?.cancelled_invoice_count || 0), averageInvoiceValue: count ? Math.round(rev / count) : 0, cashAmount: p.cash, transferAmount: p.transfer, cardAmount: p.card, otherAmount: p.other };
}
async function daily(env, store, from, to) {
  const r = await env.DB.prepare("SELECT business_date,revenue,invoice_count FROM daily_revenue_snapshots WHERE tenant_id=? AND store_id=? AND business_date>=? AND business_date<=? ORDER BY business_date").bind(store.tenant_id, store.store_id, from, to).all();
  return (r.results || []).map(x => ({ date: x.business_date, revenue: Number(x.revenue || 0), invoiceCount: Number(x.invoice_count || 0) }));
}
async function payBreak(env, tenantId, storeId, from, to) {
  const r = await env.DB.prepare("SELECT p.method,COALESCE(SUM(p.amount),0) amount FROM invoice_payment_lines p JOIN invoices i ON i.tenant_id=p.tenant_id AND i.store_id=p.store_id AND i.invoice_id=p.invoice_id WHERE i.tenant_id=? AND i.store_id=? AND i.business_date>=? AND i.business_date<=? AND i.status IN ('paid','edited') GROUP BY p.method").bind(tenantId, storeId, from, to).all();
  const out = { cash: 0, transfer: 0, card: 0, other: 0 }; for (const x of r.results || []) out[payMethod(x.method)] += Number(x.amount || 0); return out;
}
async function payBreakArray(env, store, from, to) { const p = await payBreak(env, store.tenant_id, store.store_id, from, to); return [{ method: "cash", amount: p.cash }, { method: "transfer", amount: p.transfer }, { method: "card", amount: p.card }, { method: "other", amount: p.other }]; }
async function topProducts(env, store, from, to, limit) {
  const r = await env.DB.prepare("SELECT it.product_id,it.product_name,it.product_type,COALESCE(SUM(it.quantity),0) quantity,COALESCE(SUM(it.line_total),0) revenue FROM invoice_items it JOIN invoices i ON i.tenant_id=it.tenant_id AND i.store_id=it.store_id AND i.invoice_id=it.invoice_id WHERE i.tenant_id=? AND i.store_id=? AND i.business_date>=? AND i.business_date<=? AND i.status IN ('paid','edited') GROUP BY it.product_id,it.product_name,it.product_type ORDER BY quantity DESC,revenue DESC LIMIT ?").bind(store.tenant_id, store.store_id, from, to, limit).all();
  return { from, to, items: (r.results || []).map(x => ({ productId: x.product_id || "", productName: x.product_name || "", productType: prodType(x.product_type), quantity: Number(x.quantity || 0), revenue: Number(x.revenue || 0) })) };
}
async function reportInventory(env, store, q) {
  const range = inventoryRange(store, q);
  const dayCount = daysBetween(range.from, range.to) + 1;
  const dayRow = await env.DB.prepare("SELECT COUNT(DISTINCT business_date) snapshot_days,MAX(updated_at) updated_at FROM inventory_daily_stock WHERE tenant_id=? AND store_id=? AND business_date>=? AND business_date<=?")
    .bind(store.tenant_id, store.store_id, range.from, range.to).first();
  const snapshotDays = Number(dayRow?.snapshot_days || 0);
  const r = await env.DB.prepare(`SELECT
      s.product_id,
      (SELECT x.product_name FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date>=? AND x.business_date<=? ORDER BY x.business_date DESC LIMIT 1) product_name,
      (SELECT x.unit_name FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date>=? AND x.business_date<=? ORDER BY x.business_date DESC LIMIT 1) unit_name,
      (SELECT x.opening_qty FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date>=? AND x.business_date<=? ORDER BY x.business_date ASC LIMIT 1) opening_qty,
      COALESCE(SUM(s.import_qty),0) import_qty,
      COALESCE((SELECT x.last_import_price FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date<=? AND x.last_import_price>0 ORDER BY x.business_date DESC LIMIT 1),MAX(s.last_import_price),0) last_import_price,
      COALESCE(SUM(s.manual_export_qty),0) manual_export_qty,
      COALESCE(SUM(s.sold_qty),0) sold_qty,
      COALESCE(SUM(s.total_export_qty),0) total_export_qty,
      (SELECT x.closing_qty FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date>=? AND x.business_date<=? ORDER BY x.business_date DESC LIMIT 1) closing_qty,
      COALESCE((SELECT x.min_stock FROM inventory_daily_stock x WHERE x.tenant_id=s.tenant_id AND x.store_id=s.store_id AND x.product_id=s.product_id AND x.business_date>=? AND x.business_date<=? ORDER BY x.business_date DESC LIMIT 1),MAX(s.min_stock),0) min_stock
    FROM inventory_daily_stock s
    WHERE s.tenant_id=? AND s.store_id=? AND s.business_date>=? AND s.business_date<=?
    GROUP BY s.tenant_id,s.store_id,s.product_id
    ORDER BY product_name,s.product_id`)
    .bind(range.from, range.to, range.from, range.to, range.from, range.to, range.to, range.from, range.to, range.from, range.to, store.tenant_id, store.store_id, range.from, range.to)
    .all();
  const items = (r.results || []).map(x => {
    const closingQty = Number(x.closing_qty || 0);
    const minStock = Number(x.min_stock || 0);
    return {
      productId: x.product_id || "",
      productName: x.product_name || x.product_id || "",
      unitName: x.unit_name || "",
      openingQty: Number(x.opening_qty || 0),
      importQty: Number(x.import_qty || 0),
      lastImportPrice: Number(x.last_import_price || 0),
      manualExportQty: Number(x.manual_export_qty || 0),
      soldQty: Number(x.sold_qty || 0),
      totalExportQty: Number(x.total_export_qty || 0),
      closingQty,
      minStock,
      isLowStock: minStock > 0 && closingQty <= minStock
    };
  });
  return {
    storeId: store.store_id,
    period: range.period,
    from: range.from,
    to: range.to,
    lastSyncAt: dayRow?.updated_at || store.last_sync_at || null,
    missingInventorySnapshot: snapshotDays < dayCount,
    snapshotDays,
    expectedSnapshotDays: dayCount,
    summary: {
      productCount: items.length,
      lowStockCount: items.filter(x => x.isLowStock).length,
      totalImportQty: items.reduce((sum, x) => sum + x.importQty, 0),
      totalSoldQty: items.reduce((sum, x) => sum + x.soldQty, 0),
      totalManualExportQty: items.reduce((sum, x) => sum + x.manualExportQty, 0)
    },
    items
  };
}
async function invoiceList(env, store, q) {
  const wh = ["tenant_id=?", "store_id=?"], p = [store.tenant_id, store.store_id]; const from = dateOnly(q.get("from")), to = dateOnly(q.get("to")), st = invStatus(q.get("status"), true);
  if (from) { wh.push("business_date>=?"); p.push(from); } if (to) { wh.push("business_date<=?"); p.push(to); } if (st) { wh.push("status=?"); p.push(st); }
  const page = clamp(q.get("page"), 1, 999999, 1), size = clamp(q.get("pageSize"), 1, 100, 50), off = (page - 1) * size, sql = wh.join(" AND ");
  const total = await env.DB.prepare(`SELECT COUNT(*) count FROM invoices WHERE ${sql}`).bind(...p).first();
  const r = await env.DB.prepare(`SELECT invoice_id,invoice_version,status,business_date,table_name,cashier,paid_at,subtotal,discount,total,payment_method FROM invoices WHERE ${sql} ORDER BY business_date DESC,paid_at DESC,invoice_id DESC LIMIT ? OFFSET ?`).bind(...p, size, off).all();
  return { page, pageSize: size, totalItems: Number(total?.count || 0), items: (r.results || []).map(x => ({ invoiceId: x.invoice_id, invoiceVersion: Number(x.invoice_version || 1), status: x.status, businessDate: x.business_date, tableName: x.table_name || "", cashier: x.cashier || "", paidAt: x.paid_at || null, subtotal: Number(x.subtotal || 0), discount: Number(x.discount || 0), total: Number(x.total || 0), paymentMethod: invMethod(x.payment_method, []) })) };
}
async function invoiceDetail(env, store, invoiceId) {
  const i = await env.DB.prepare("SELECT * FROM invoices WHERE tenant_id=? AND store_id=? AND invoice_id=?").bind(store.tenant_id, store.store_id, invoiceId).first(); if (!i) notFound("invoice_not_found");
  const items = await env.DB.prepare("SELECT * FROM invoice_items WHERE tenant_id=? AND store_id=? AND invoice_id=? ORDER BY line_id").bind(store.tenant_id, store.store_id, invoiceId).all();
  const pays = await env.DB.prepare("SELECT method,amount,created_at FROM invoice_payment_lines WHERE tenant_id=? AND store_id=? AND invoice_id=? ORDER BY created_at,payment_id").bind(store.tenant_id, store.store_id, invoiceId).all();
  return { tenantId: i.tenant_id, storeId: i.store_id, invoiceId: i.invoice_id, invoiceVersion: Number(i.invoice_version || 1), status: i.status, tableName: i.table_name || "", cashier: i.cashier || "", openedAt: i.opened_at || null, paidAt: i.paid_at || null, businessDate: i.business_date, subtotal: Number(i.subtotal || 0), discount: Number(i.discount || 0), total: Number(i.total || 0), paymentMethod: invMethod(i.payment_method, []), discountNote: i.discount_note || "", payments: (pays.results || []).map(x => ({ method: payMethod(x.method), amount: Number(x.amount || 0), createdAt: x.created_at || null })), items: (items.results || []).map(x => ({ lineId: x.line_id, productId: x.product_id || "", productName: x.product_name || "", productType: prodType(x.product_type), unitName: x.unit_name || "", quantity: Number(x.quantity || 0), unitPrice: Number(x.unit_price || 0), lineTotal: Number(x.line_total || 0), note: x.note || "" })) };
}
async function login(env, b) {
  const tenantId = text(b.tenantId), username = text(b.username).toLowerCase(); if (!tenantId || !username) bad("invalid_login");
  const u = await env.DB.prepare("SELECT * FROM manager_users WHERE tenant_id=? AND username=? AND is_active=1").bind(tenantId, username).first();
  if (!u || u.password_hash !== await hashPassword(String(b.password || ""), u.password_salt)) unauth("invalid_login");
  return newSession(env, u);
}
async function refresh(env, b) {
  const rt = text(b.refreshToken); if (!rt) unauth("invalid_refresh_token");
  const h = await sha256Text(rt);
  const s = await env.DB.prepare("SELECT s.*,u.username,u.display_name,u.is_active FROM manager_sessions s JOIN manager_users u ON u.user_id=s.user_id WHERE s.refresh_token_hash=? AND s.revoked_at IS NULL").bind(h).first();
  if (!s || s.is_active !== 1 || new Date(s.refresh_expires_at).getTime() <= Date.now()) unauth("invalid_refresh_token");
  await revoke(env, s.session_id); return newSession(env, s);
}
async function newSession(env, u) {
  const accessToken = token(48), refreshToken = token(64), accessExp = new Date(Date.now() + ACCESS_TTL * 1000).toISOString(), refreshExp = new Date(Date.now() + REFRESH_TTL * 1000).toISOString();
  await env.DB.prepare("INSERT INTO manager_sessions (session_id,user_id,tenant_id,access_token_hash,refresh_token_hash,access_expires_at,refresh_expires_at,last_seen_at) VALUES (?,?,?,?,?,?,?,?)")
    .bind(crypto.randomUUID(), u.user_id, u.tenant_id, await sha256Text(accessToken), await sha256Text(refreshToken), accessExp, refreshExp, now()).run();
  return { accessToken, refreshToken, tokenType: "Bearer", expiresIn: ACCESS_TTL, refreshExpiresIn: REFRESH_TTL, user: { userId: u.user_id, tenantId: u.tenant_id, username: u.username, displayName: u.display_name || u.username } };
}
async function manager(request, env, allowExpired = false) {
  const tokenValue = text((request.headers.get("Authorization") || "").replace(/^Bearer\s+/i, "")); if (!tokenValue) unauth("missing_token");
  const s = await env.DB.prepare("SELECT s.*,u.is_active FROM manager_sessions s JOIN manager_users u ON u.user_id=s.user_id WHERE s.access_token_hash=? AND s.revoked_at IS NULL").bind(await sha256Text(tokenValue)).first();
  if (!s || s.is_active !== 1) unauth("invalid_token");
  if (!allowExpired && new Date(s.access_expires_at).getTime() <= Date.now()) unauth("token_expired");
  await env.DB.prepare("UPDATE manager_sessions SET last_seen_at=? WHERE session_id=?").bind(now(), s.session_id).run(); return s;
}
async function revoke(env, id) { await env.DB.prepare("UPDATE manager_sessions SET revoked_at=? WHERE session_id=?").bind(now(), id).run(); }
async function stores(env, tenantId) {
  const r = await env.DB.prepare("SELECT store_id,name,timezone,revenue_cloud_enabled,last_sync_at FROM stores WHERE tenant_id=? ORDER BY name,store_id").bind(tenantId).all();
  return { stores: (r.results || []).map(x => ({ storeId: x.store_id, name: x.name || x.store_id, timezone: x.timezone || "Asia/Ho_Chi_Minh", enabled: x.revenue_cloud_enabled === 1, lastSyncAt: x.last_sync_at || null })) };
}
async function reportStore(env, tenantId, storeId) {
  const s = storeId ? await env.DB.prepare("SELECT * FROM stores WHERE tenant_id=? AND store_id=? AND revenue_cloud_enabled=1").bind(tenantId, storeId).first() : await env.DB.prepare("SELECT * FROM stores WHERE tenant_id=? AND revenue_cloud_enabled=1 ORDER BY name,store_id LIMIT 1").bind(tenantId).first();
  if (!s) notFound("store_not_found"); return s;
}
async function cleanupNoncesMaybe(env) { if (Math.random() < 0.05) await cleanupNonces(env); }
async function cleanupNonces(env) { await env.DB.prepare("DELETE FROM sync_nonces WHERE expires_at<? OR created_at<?").bind(now(), new Date(Date.now() - NONCE_CLEANUP_SECONDS * 1000).toISOString()).run(); }
async function logSync(env, tenantId, storeId, type, msg, hash, requestTimestamp = "", requestNonce = "") {
  await env.DB.prepare("INSERT INTO sync_logs (log_id,tenant_id,store_id,event_type,message,body_sha256,request_timestamp,request_nonce) VALUES (?,?,?,?,?,?,?,?)")
    .bind(crypto.randomUUID(), tenantId || "", storeId || "", type, msg || "", hash || "", requestTimestamp || "", requestNonce || "").run();
}
async function audit(env, tenantId, storeId, actor, action, msg) { await env.DB.prepare("INSERT INTO audit_logs (audit_id,tenant_id,store_id,actor_type,action,message) VALUES (?,?,?,?,?,?)").bind(crypto.randomUUID(), tenantId || "", storeId || "", actor || "", action, msg || "").run(); }

function requireAdmin(request, env) { if (!env.ADMIN_SECRET || (request.headers.get("Authorization") || "") !== `Bearer ${env.ADMIN_SECRET}`) unauth("unauthorized"); }
async function readJson(request) { try { const t = await request.text(); return t ? JSON.parse(t) : {}; } catch { bad("invalid_json"); } }
function parseJson(bytes) { try { const t = new TextDecoder().decode(bytes); return t ? JSON.parse(t) : {}; } catch { bad("invalid_json"); } }
async function hmac(secret, ts, nonce, body) { const p = enc(`${ts}|${nonce}|`), msg = new Uint8Array(p.length + body.length); msg.set(p); msg.set(body, p.length); const key = await crypto.subtle.importKey("raw", enc(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]); return hex(new Uint8Array(await crypto.subtle.sign("HMAC", key, msg))); }
async function hashPassword(password, salt) {
  const key = await crypto.subtle.importKey("raw", enc(String(password || "")), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits(
    { name: "PBKDF2", hash: "SHA-256", salt: enc(String(salt || "")), iterations: PASSWORD_PBKDF2_ITERATIONS },
    key,
    256
  );
  return hex(new Uint8Array(bits));
}
async function sha256Text(v) { return sha256(enc(String(v || ""))); }
async function sha256(bytes) { return hex(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes))); }
function enc(v) { return new TextEncoder().encode(v); }
function hex(bytes) { return [...bytes].map(b => b.toString(16).padStart(2, "0")).join(""); }
function ctEqual(a, b) { if (a.length !== b.length) return false; let d = 0; for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i); return d === 0; }
function token(n) { const b = new Uint8Array(n); crypto.getRandomValues(b); return btoa(String.fromCharCode(...b)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, ""); }
function json(p, status = 200) { return new Response(JSON.stringify(p), { status, headers: H }); }
function privacyPolicy() {
  return new Response(`<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>BKPos Revenue Privacy Policy</title>
  <style>
    body{font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;line-height:1.55;color:#142033;background:#f7f9fc;margin:0}
    main{max-width:820px;margin:0 auto;padding:40px 20px}
    article{background:#fff;border:1px solid #dbe4f0;border-radius:16px;padding:28px;box-shadow:0 8px 28px rgba(20,32,51,.08)}
    h1{margin-top:0;color:#0b2034}
    h2{margin-top:28px;color:#153f73}
    p,li{font-size:16px}
    .muted{color:#5c6b80}
  </style>
</head>
<body>
  <main>
    <article>
      <h1>BKPos Revenue Privacy Policy</h1>
      <p class="muted">Last updated: May 23, 2026</p>
      <p>BKPos Revenue is a business reporting application used by restaurant and coffee shop owners to view revenue reports synchronized from the BKPos system.</p>

      <h2>Information We Process</h2>
      <p>The app may display business data such as store name, revenue totals, invoices, payment summaries, and product sales reports. This data is provided by the store owner's BKPos system or by a demo cloud account for Apple review.</p>

      <h2>Account Information</h2>
      <p>Users sign in with credentials issued by the BKPos provider or store administrator. Passwords are transmitted over HTTPS and are not stored in plain text by the cloud service.</p>

      <h2>Information We Do Not Collect</h2>
      <ul>
        <li>We do not collect contacts, photos, camera data, microphone data, or precise location.</li>
        <li>We do not use third-party advertising SDKs.</li>
        <li>We do not sell personal information.</li>
      </ul>

      <h2>Data Usage</h2>
      <p>Business data is used only to provide reporting features to authorized users, including daily revenue, monthly revenue, payment breakdowns, top products, and invoice details.</p>

      <h2>Data Security</h2>
      <p>Communication between the app and BKPos cloud endpoints uses HTTPS. Synchronization requests are protected with signed requests to reduce unauthorized access.</p>

      <h2>Data Retention</h2>
      <p>Business data is retained for reporting purposes until the store owner requests deletion or disables the cloud reporting feature.</p>

      <h2>Contact</h2>
      <p><strong>Bao Khang Laptop</strong><br>
      Phone/Zalo: 0396 529 103<br>
      Email: tinhthanhdo1990@gmail.com</p>
    </article>
  </main>
</body>
</html>`, {
    status: 200,
    headers: {
      "content-type": "text/html; charset=utf-8",
      "cache-control": "public, max-age=3600"
    }
  });
}
function bad(error) { throw new Response(JSON.stringify({ error, message: error }), { status: 400, headers: H }); }
function unauth(error) { throw new Response(JSON.stringify({ error, message: error }), { status: 401, headers: H }); }
function notFound(error) { throw new Response(JSON.stringify({ error, message: error }), { status: 404, headers: H }); }
function conflict(error, extra = {}) { throw new Response(JSON.stringify({ error, message: error, ...extra }), { status: 409, headers: H }); }
function text(v) { return String(v || "").trim(); }
function id(v) { return text(v).toUpperCase(); }
function digits(v) { return text(v).replace(/\D/g, ""); }
function invStatus(v, all = false) { const t = text(v).toLowerCase(); if (all && (!t || t === "all")) return ""; return ["paid", "edited", "cancelled"].includes(t) ? t : ""; }
function payMethod(v) { const t = text(v).toLowerCase(); if (PAY_METHODS.has(t)) return t; if (t === "1") return "cash"; if (t === "2") return "card"; if (t === "3") return "transfer"; return "other"; }
function invMethod(v, p) { if (p.length >= 2) return "split"; const t = text(v).toLowerCase(); if (INV_METHODS.has(t)) return t; return p.length === 1 ? p[0].method : payMethod(t); }
function prodType(v) { const t = text(v).toLowerCase(); if (PROD_TYPES.has(t)) return t; if (t === "0") return "food"; if (t === "1") return "drink"; if (t === "2") return "other"; return "drink"; }
function iso(v) { const t = text(v); if (!t) return null; const d = new Date(t); return Number.isNaN(d.getTime()) ? null : d.toISOString(); }
function dateOnly(v) { const t = text(v); return /^\d{4}-\d{2}-\d{2}$/.test(t) ? t : ""; }
function reqDate(v, n) { const d = dateOnly(v); if (!d) bad(`invalid_${n}`); return d; }
function reqMonth(v) { const t = text(v); if (!/^\d{4}-\d{2}$/.test(t)) bad("invalid_month"); return t; }
function dateFrom(v) { return v ? dateOnly(String(v).slice(0, 10)) : ""; }
function utcDate(d) { return d.toISOString().slice(0, 10); }
function localDate(timezone, offsetDays = 0) {
  const d = new Date(Date.now() + offsetDays * 86400000);
  const parts = new Intl.DateTimeFormat("en", {
    timeZone: text(timezone) || "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit"
  }).formatToParts(d);
  const get = type => parts.find(x => x.type === type)?.value || "";
  return `${get("year")}-${get("month")}-${get("day")}`;
}
function addDays(d, n) { const x = new Date(`${d}T00:00:00Z`); x.setUTCDate(x.getUTCDate() + n); return utcDate(x); }
function nextMonth(m) { const x = new Date(`${m}-01T00:00:00Z`); x.setUTCMonth(x.getUTCMonth() + 1); return utcDate(x); }
function inventoryRange(store, q) {
  const period = text(q.get("period") || "today").toLowerCase();
  const today = localDate(store.timezone || "Asia/Ho_Chi_Minh");
  let from = "", to = "";
  if (period === "custom") {
    from = reqDate(q.get("from"), "from");
    to = reqDate(q.get("to"), "to");
  } else if (period === "yesterday") {
    from = to = addDays(today, -1);
  } else if (period === "last7") {
    from = addDays(today, -6);
    to = today;
  } else if (period === "thismonth") {
    from = `${today.slice(0, 7)}-01`;
    to = today;
  } else if (period === "lastmonth") {
    const firstThisMonth = `${today.slice(0, 7)}-01`;
    const lastMonthDay = addDays(firstThisMonth, -1);
    from = `${lastMonthDay.slice(0, 7)}-01`;
    to = lastMonthDay;
  } else if (!period || period === "today") {
    from = to = today;
  } else {
    bad("invalid_inventory_period");
  }
  if (from > to) bad("invalid_date_range");
  return { period: period || "today", from, to };
}
function daysBetween(from, to) { return Math.max(0, Math.round((new Date(`${to}T00:00:00Z`) - new Date(`${from}T00:00:00Z`)) / 86400000)); }
function money(v) { const n = Number(v || 0); return Number.isFinite(n) ? Math.round(Math.max(0, n)) : 0; }
function qty(v, allowNegative = false) { const n = Number(v || 0); if (!Number.isFinite(n)) return 0; return allowNegative ? n : Math.max(0, n); }
function clamp(v, min, max, fb) { const n = Number(v); return Number.isFinite(n) ? Math.min(max, Math.max(min, Math.trunc(n))) : fb; }
function now() { return new Date().toISOString(); }

export const __test = {
  reportToday,
  reportRange,
  reportMonth,
  topProducts,
  reportOpenTables,
  reportInventory,
  syncInventoryDaily,
  invoiceList,
  invoiceDetail
};
