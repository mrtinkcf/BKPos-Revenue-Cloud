# BKPos Revenue Cloud Worker

Worker rieng cho tinh nang Revenue Cloud. Khong dung chung voi worker license `bkpos-lic-bk`.

## Files

- `worker.js`: Cloudflare Worker API.
- `migrations/0001_initial.sql`: D1 schema V1.
- `wrangler.toml`: cau hinh mau, can thay `database_id` sau khi tao D1.

## Secrets can thiet

```powershell
wrangler secret put ADMIN_SECRET
```

`ADMIN_SECRET` chi dung cho KeyGen/noi bo khi provision tenant/store.

## Tao D1 va apply migration

```powershell
wrangler d1 create bkpos-revenue-cloud
# copy database_id vao wrangler.toml
wrangler d1 migrations apply bkpos-revenue-cloud --remote
```

## Provision test

```powershell
$body = @{ tenantName='Bao Khang'; storeName='Quan demo'; managerUsername='admin'; managerPassword='123456' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri https://<worker>/admin/revenue-cloud/provision -Headers @{ Authorization='Bearer <ADMIN_SECRET>' } -Body $body -ContentType 'application/json'
```

Response tra ve `tenantId`, `storeId`, `syncKey`. `syncKey` chi cau hinh trong BKPos Mobile Server, khong dua vao app Revenue.

## Luu y

- Sync request bat buoc dung HMAC canonical theo spec: `timestamp|nonce|raw UTF-8 body bytes`.
- `/reports/range`, `/reports/month`, `/reports/today` tinh payment totals tu `invoice_payment_lines`, khong tu `invoice.payment_method`.
- `sync_nonces` co TTL 10 phut va cleanup theo cron/opportunistic.
