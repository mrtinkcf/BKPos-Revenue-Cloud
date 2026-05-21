# BKPos Revenue Cloud V1 - Spec hop dong ky thuat

> Tai lieu nay la can cu ky thuat rang buoc truoc khi code tinh nang theo doi doanh thu online Android/iPhone.
> Moi thay doi ve kien truc, bao mat, license, DB, sync va app quan ly phai bam theo spec nay.
> Mac dinh: khong code neu chua duoc chu du an kiem duyet va chot.

---

**Phien ban tai lieu:** 1.7  
**Ngay lap:** 2026-05-20  
**Du an:** BKPos Revenue Cloud - ung dung quan ly doanh thu online  
**Nen tang hien co:** BKPos Desktop, BKPos Mobile Server API, Firebird DB cuc bo tai quan  
**Trang thai:** Cho chu du an kiem duyet

---

## Changelog

- `v1.1` - Bo sung mapping Firebird -> cloud payload bat buoc, chot module sync nam trong `BKPos.Mobile.Api`, chuan hoa HMAC canonical bytes, xac nhan iOS V1 build bang Xcode Cloud.
- `v1.2` - Bo sung lifecycle `daily_revenue_snapshots`, cleanup `sync_nonces`, thoi han token App Revenue, UX offline, loai chart V1 va mac dinh `CloudSync.IntervalSeconds = 300`.
- `v1.3` - Chuan hoa enum payment method, sua logic pie/donut chart theo payment lines, bo sung Manager API response schema toi thieu va pham vi cache offline.
- `v1.4` - Bo sung timezone/date cho reports, response schema `/reports/month`, va quy tac payment totals trong summary phai tinh tu payment lines.
- `v1.5` - Bo sung `paymentBreakdown` cho `/reports/range` va gioi han cache chi tiet hoa don offline toi da 100 invoice detail theo `storeId + userId`.
- `v1.6` - Bat buoc hash mat khau manager bang PBKDF2-HMAC-SHA256 100000 iterations va sync log luu `timestamp/nonce` khi signature sai.
- `v1.7` - Chot kenh iOS giai doan dau: GitHub Actions macOS build .NET MAUI iOS va upload TestFlight; Xcode Cloud de du phong/sau.

---

## 1. Muc tieu

Xay dung them mot he thong xem doanh thu online de chu quan co the theo doi doanh thu o bat cu dau tren Android va iPhone.

Muc tieu cot loi:

- Giu nguyen cau truc BKPos hien tai tai quan.
- Khong mo Firebird DB hoac PC quan truc tiep ra Internet.
- BKPos Mobile Server API co them module sync doanh thu len cloud.
- App quan ly doanh thu doc du lieu tu cloud, khong doc truc tiep DB quan.
- Tinh nang cloud sync la tuy chon, mac dinh tat.
- Khach hang dang dung ban cu khong bi anh huong neu khong bat sync.

---

## 2. Kien truc da chot

```text
BKPos Desktop / BKPos Mobile Server tai quan
        |
        | Doc hoa don da hoan tat tu Firebird cuc bo
        | HTTPS sync dinh ky neu duoc bat
        v
Cloudflare Worker API
        |
        v
Cloudflare D1 Database
        |
        v
BKPos Revenue App Android/iPhone
```

Nguyen tac:

- Desktop va Mobile Order van ban hang/offline binh thuong.
- Cloud sync chi la module phu, khong duoc block ban hang.
- App Revenue chi xem bao cao, khong can thiep order/thanh toan/in an trong V1.
- Moi loi sync phai log rieng, khong popup lam gian doan thu ngan.

---

## 3. Pham vi V1

### 3.1 Co trong V1

- Bat/tat dong bo doanh thu online.
- Cau hinh Store ID va Sync Key tren BKPos Mobile Server hoac Desktop settings.
- Sync hoa don da thanh toan len cloud.
- Sync hoa don bi huy len cloud.
- Sync hoa don da sua sau thanh toan len cloud theo version.
- Sync chi tiet mon trong hoa don.
- Sync giam gia, ghi chu khuyen mai, phuong thuc thanh toan.
- App Revenue Android/iPhone xem:
  - Doanh thu hom nay.
  - Doanh thu theo khoang ngay.
  - Doanh thu theo thang.
  - So hoa don.
  - Trung binh/hoa don.
  - Tien mat/chuyen khoan.
  - Top mon ban chay.
  - Danh sach hoa don.
  - Hoa don da huy.
  - Trang thai sync gan nhat cua quan.

### 3.2 Khong co trong V1

- Khong sua order tu app Revenue.
- Khong thanh toan tu app Revenue.
- Khong in an tu app Revenue.
- Khong truy cap Firebird truc tiep qua Internet.
- Khong dung Cloudflare Tunnel lam kenh chinh cho app Revenue.
- Khong bat buoc khach cu phai dung cloud.

---

## 4. Tuy chon sync

Trong cau hinh phai co:

- `CloudSync.Enabled`: Bat/tat dong bo doanh thu online.
- `CloudSync.StoreId`: Ma cua hang/chi nhanh.
- `CloudSync.SyncKey`: Khoa dong bo rieng cua cua hang.
- `CloudSync.IntervalSeconds`: Chu ky dong bo.
- `CloudSync.LastSyncAt`: Lan sync gan nhat.
- `CloudSync.LastError`: Loi sync gan nhat.
- Nut `Dong bo ngay`.

Gia tri mac dinh:

```text
CloudSync.Enabled = false
CloudSync.IntervalSeconds = 300
```

Neu sync tat:

- Khong tao request cloud.
- Khong yeu cau cau hinh Store ID/Sync Key.
- Khong anh huong ban hang.
- Khong anh huong license desktop/mobile hien tai.

Neu sync duoc bat:

- `CloudSync.IntervalSeconds` mac dinh `300` giay (5 phut).
- Gia tri toi thieu UI cho phep: `60` giay.
- Gia tri toi da UI cho phep: `3600` giay.
- Nut `Dong bo ngay` duoc phep chay ngoai chu ky, nhung phai co debounce/chong bam lien tuc toi thieu `10` giay.
- Worker/API loi khong duoc rut ngan interval tu dong den muc spam Cloudflare.

---

## 5. Bao mat

### 5.1 Nguyen tac bao mat

- Khong luu `ADMIN_SECRET` trong app Revenue.
- Khong dua Sync Key vao app Revenue cua chu quan.
- Sync Key chi nam tren may chu quan/BKPos Mobile Server.
- App Revenue dang nhap bang tai khoan quan ly rieng.
- Moi request sync phai co chu ky HMAC.
- Cloud phai chong replay bang timestamp + nonce.
- Cloud phai chong gui trung bang idempotency key.
- Token app Revenue phai co thoi han ngan va refresh token.
- Co the thu hoi tai khoan/thiet bi quan ly.

### 5.2 Ky request sync

Moi request tu quan len cloud can co header:

```text
X-BKPOS-Tenant: <tenantId>
X-BKPOS-Store: <storeId>
X-BKPOS-Timestamp: <unix timestamp seconds UTC>
X-BKPOS-Nonce: <uuid v4 lowercase>
X-BKPOS-Signature: <hex lowercase HMAC-SHA256>
```

Cloud Worker kiem tra:

- Tenant/store ton tai.
- Store dang duoc bat Revenue Cloud.
- Signature hop le.
- Timestamp khong qua han.
- Nonce chua su dung.
- Payload dung schema.

### 5.3 Canonical HMAC bat buoc

Khong duoc de Local Server va Cloud Worker tu hieu khac nhau ve chu ky.

Quy dinh canonical:

```text
timestamp = Unix time seconds UTC, int64, dang chuoi so thap phan.
nonce = UUID v4 lowercase, vi du: 9b31e8c3-0891-4a4e-9d30-2e9b97c1f1a5.
bodyBytes = exact UTF-8 request body bytes duoc gui len HTTP, JSON minified, no BOM, no indentation.
prefixBytes = UTF8(timestamp + "|" + nonce + "|").
messageBytes = prefixBytes + bodyBytes.
signature = HMACSHA256(syncKeyBytes, messageBytes) -> hex lowercase.
```

Quy tac serialize body:

- Local Server dung cung payload DTO de serialize JSON minified.
- Worker phai doc raw request body bytes de verify, khong duoc parse roi serialize lai truoc khi verify.
- Content-Type bat buoc: `application/json; charset=utf-8`.
- Clock skew toi da: `300` giay.
- Nonce TTL: toi thieu `10` phut va phai unique theo `tenantId + storeId + nonce`.
- Signature compare phai constant-time.
- Neu signature sai, Worker tra `401 invalid_signature` va ghi audit log toi thieu: tenant, store, timestamp, nonce, body SHA256.

### 5.4 Hash mat khau manager

Mat khau tai khoan App Revenue/manager la du lieu nhay cam, khong duoc hash bang SHA-256 mot lan.

Quy dinh V1:

- Hash password bang `PBKDF2-HMAC-SHA256`.
- Salt random rieng cho tung user, toi thieu 16 bytes entropy.
- So vong lap toi thieu: `100000`.
- Output: 256-bit hash, luu dang hex lowercase trong `manager_users.password_hash`.
- Salt luu rieng trong `manager_users.password_salt`.
- Khong luu password plain text, khong log password, khong tra password qua API.

### 5.5 Token va session App Revenue

Token App Revenue phai co thoi han cu the, khong de dev tu quy uoc.

Quy dinh V1:

- Access token TTL: `60` phut.
- Refresh token TTL: `30` ngay.
- Refresh token phai luu dang hash tren server, khong luu plain text.
- Moi lan refresh thanh cong phai rotate refresh token moi va vo hieu hoa token cu.
- Logout phai xoa/thu hoi session server-side trong `manager_sessions`.
- Doi mat khau hoac admin revoke user phai vo hieu hoa tat ca session cua user do.
- App bi mat mang nhung access token con han thi chi duoc xem cache local, khong duoc goi API that bai lien tuc.

---

## 6. License va cap key

### 6.1 Co khac gi khach cu hien tai khong?

Co khac ve quyen tinh nang, nhung khong lam hong key cu.

Khach cu hien tai:

- Van dung license Desktop/Mobile nhu hien tai.
- Neu license khong co feature `RevenueCloud` thi tinh nang sync online mac dinh tat va khong su dung duoc.
- Ban cap nhat phan mem khong lam mat license cu.

Khach mua them xem doanh thu online:

- Duoc cap them quyen `RevenueCloud` theo store/tenant.
- Duoc tao `Tenant ID`, `Store ID`, `Sync Key` va tai khoan quan ly.
- App Revenue dang nhap bang tai khoan quan ly, khong nhap Sync Key.

### 6.2 Huong cap quyen de xuat

Co 2 cach, uu tien cach A.

#### Cach A - Mo rong license hien tai bang feature

Trong KeyGen them feature:

```text
features: ["desktop", "mobile_order", "revenue_cloud"]
```

Hoac them cot/field:

```text
LicenseType = Desktop | MobileOrder | RevenueCloud | Bundle
```

Uu diem:

- Giu chung he thong license hien tai.
- Khach cu khong co `revenue_cloud` thi khong bi anh huong.
- De thu hoi tinh nang Revenue Cloud qua Cloudflare Worker.
- De ban theo goi: Desktop only, Mobile Order, Revenue Cloud, Full Bundle.

#### Cach B - License rieng cho Revenue Cloud

Tao key rieng cho tinh nang Revenue Cloud.

Uu diem:

- Tach bach thu phi tinh nang online.

Nhuoc diem:

- Quan ly phuc tap hon.
- De nham giua key Desktop, key Mobile Order va key Revenue Cloud.

Ket luan: V1 chon Cach A.

### 6.3 KeyGen can bo sung

- Them loai license/feature `RevenueCloud`.
- Them man hinh hoac tab quan ly tenant/store cloud.
- Tao Store ID va Sync Key.
- Thu hoi Revenue Cloud rieng ma khong can thu hoi Desktop neu can.
- Hien thi cot `Loai license` va `Tinh nang` ro rang.
- Khong hien Sync Key cho khach neu khong can; chi copy khi cau hinh tai quan.

### 6.4 Quy trinh cap Revenue Cloud bat buoc qua BKPos KeyGen

Tat ca thao tac cap, gia han, doi key, khoa, mo khoa va thu hoi tinh nang `RevenueCloud` phai thuc hien tren UI `BKPos.KeyGen.exe`.

Khong duoc yeu cau nguoi van hanh thao tac tay trong Cloudflare dashboard, D1 database, wrangler CLI hoac sua JSON thu cong cho tung khach.

Luong thao tac tren KeyGen:

1. Mo `BKPos.KeyGen.exe`.
2. Chon khach hang hoac license hien co.
3. Tick/bat tinh nang `RevenueCloud`.
4. Bam `Cap Revenue Cloud`.
5. KeyGen goi Cloudflare Worker admin API de:
   - Tao hoac lay `Tenant ID`.
   - Tao hoac lay `Store ID`.
   - Sinh `Sync Key` bao mat.
   - Gan feature `RevenueCloud` vao license/khach hang.
   - Luu trang thai Revenue Cloud tren cloud.
6. KeyGen hien thi ket qua:
   - `Tenant ID`.
   - `Store ID`.
   - `Sync Key`.
   - Trang thai `Da cap Revenue Cloud`.
7. Khi cau hinh tai quan, chi copy `Store ID` va `Sync Key` vao BKPos Mobile Server/Desktop settings.

Yeu cau bao mat cho KeyGen:

- `ADMIN_SECRET` hoac admin token chi nam trong KeyGen noi bo, khong dua vao app Revenue va khong dua cho khach.
- Sync Key chi hien/copy khi can cau hinh tai quan.
- Co nut doi Sync Key neu nghi ngo bi lo.
- Co nut thu hoi Revenue Cloud ma khong bat buoc thu hoi Desktop license.
- Moi thao tac cap/thu hoi phai ghi audit log tren cloud.
- Neu Worker admin API loi thi KeyGen phai hien popup loi ro rang va khong ghi trang thai gia.

Dieu kien nghiem thu KeyGen:

- Cap moi Revenue Cloud tu UI thanh cong.
- Cap lai cho khach da co tenant/store khong tao trung lung tung.
- Doi Sync Key lam key cu het hieu luc.
- Thu hoi Revenue Cloud lam CloudSync tai quan sync that bai co kiem soat, POS van ban hang binh thuong.
- Khach khong co `RevenueCloud` khong cau hinh sync duoc.

---

## 7. Tuong thich khach hang dang dung ban cu

Yeu cau bat buoc:

- Update phan mem khong duoc ghi de DB khach.
- Update phan mem khong duoc xoa template in.
- Update phan mem khong duoc doi cau hinh may in.
- Cloud sync mac dinh tat.
- Neu thieu bang sync local thi tu tao mem, loi tao bang khong duoc chan ban hang.
- Neu cloud loi thi chi ghi log sync, khong popup lien tuc.
- Neu khach khong mua Revenue Cloud thi toan bo phan order/thanh toan/in an van nhu cu.

---

## 8. Du lieu dong bo

### 8.1 Invoice payload

Moi hoa don sync len cloud gom:

```json
{
  "tenantId": "...",
  "storeId": "...",
  "invoiceId": "...",
  "invoiceVersion": 1,
  "status": "paid|cancelled|edited",
  "tableName": "Ban A1",
  "cashier": "Administrator",
  "openedAt": "2026-05-20T08:00:00+07:00",
  "paidAt": "2026-05-20T08:30:00+07:00",
  "subtotal": 1000000,
  "discount": 100000,
  "total": 900000,
  "paymentMethod": "cash|transfer|card|split|other",
  "payments": [],
  "discountNote": "Mung khai truong...",
  "items": []
}
```

### 8.1.1 Payment enum bat buoc

Invoice-level `paymentMethod`:

```text
cash | transfer | card | split | other
```

Payment-line `method`:

```text
cash | transfer | card | other
```

Quy tac:

- `split` chi duoc dung o invoice-level `paymentMethod` khi hoa don co tu 2 dong payment tro len.
- `split` khong bao gio la gia tri cua payment-line `method`.
- Khong dung gia tri ghep bang dau slash nhu `card/other`.
- Neu khong xac dinh duoc method, dung `other`.
- Front-end, Worker, D1 schema va report phai dung dung enum tren.

### 8.2 Invoice item

```json
{
  "lineId": "...",
  "productId": "...",
  "productName": "Bac suu",
  "productType": "food|drink|other",
  "unitName": "ly",
  "quantity": 2,
  "unitPrice": 20000,
  "lineTotal": 40000,
  "note": "it da"
}
```

### 8.2.1 Payment line

```json
{
  "method": "cash|transfer|card|other",
  "amount": 900000,
  "createdAt": "2026-05-20T08:30:00+07:00"
}
```

### 8.3 Anh xa Firebird -> Cloud Payload bat buoc

Day la mapping theo schema Firebird hien co trong `publish/DB/DEMO.FDB` va theo resolver code hien tai. Dev khong duoc tu y doi ten bang/cot cloud neu chua sua spec.

#### 8.3.1 Bang/cot Firebird hien co

Hoa don:

- Header: `TDONHANG`.
- Chi tiet: `TDONHANGCHITIET`.
- Ban: `DBAN`.
- Nhan vien/thu ngan: `SUSER`.
- Mat hang: `DMATHANG`.
- Don vi tinh: `DDONVITINH`.
- Nhom mat hang: `DNHOMMATHANG`.
- Thanh toan chi tiet: `BKPOS_PAYMENT`.
- Snapshot khuyen mai: `BKPOS_ORDER_DISCOUNT`.

Cot chinh cua `TDONHANG` trong DB hien tai:

```text
ID, NOTE, STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, SORTORDER,
USERCREATEDID, DBANID, BATDAU, KETTHUC, TIENNUOC, TIENBAN, TONGCONG,
TILEGIAMGIA, TONGGIAMGIA, DATHANHTOAN, NGAY, SO, USERTHANHTOANID,
NHOMGUID, TILEGIAMGIADOUONG, TONGGIAMGIADOUONG, TONGCONGGIAMGIA,
PHIDICHVU, TILEPHIDICHVU, DKHACHHANGID, LANIN, CNHANVIENID,
DAINPHIEUINTHU, CONNO, DATHANHTOANNO, KHACHDUA, TRALAI, SOORDER,
GIAMTHEOTIEN
```

Cot chinh cua `TDONHANGCHITIET` trong DB hien tai:

```text
ID, NOTE, STATUS, USERMODIFIEDID, TIMEMODIFIED, TIMECREATED, SORTORDER,
USERCREATEDID, TDONHANGID, DMATHANGID, SOLUONG, DONGIA, THANHTIEN,
TENHANG, GIAVON
```

#### 8.3.2 Mapping invoice header

| Cloud field | Firebird source | Ghi chu bat buoc |
|---|---|---|
| `invoiceId` | `TDONHANG.ID` | Fallback resolver: `MADONHANG`, `AUTOID`, `ID`; DB hien tai dung `ID`. |
| `invoiceVersion` | `BKPOS_CLOUD_SYNC_VERSION.VERSION` | Bat buoc tao bang rieng; khong duoc chi dua vao `TDONHANG.TIMEMODIFIED`. |
| `status` | `TDONHANG.STATUS`, `TDONHANG.DATHANHTOAN`, `BKPOS_CLOUD_SYNC_VERSION.STATUS` | `paid` khi `DATHANHTOAN in (1,30)` va `STATUS=30`; `cancelled` khi `STATUS=0`; `edited` khi version tang do sua hoa don da thanh toan. |
| `tableName` | `TDONHANG.DBANID` -> `DBAN.ID` -> `DBAN.NAME` | Neu khong join duoc thi de chuoi rong, khong fail sync. |
| `cashier` | `TDONHANG.USERTHANHTOANID` fallback `USERCREATEDID` -> `SUSER.ID` -> `SUSER.NAME` | Neu khong join duoc thi de chuoi rong. |
| `openedAt` | `TDONHANG.BATDAU` | Fallback resolver: `THOIGIANDEN`, `NGAYTAO`, `NGAY`. |
| `paidAt` | `TDONHANG.KETTHUC` | Fallback resolver: `GIORA`, `THOIGIANRA`, `THOIGIANTHANHTOAN`; neu null thi dung `NGAY`/`BATDAU` theo thu tu. |
| `businessDate` | `TDONHANG.NGAY` | Dung de bao cao theo ngay ban hang. |
| `subtotal` | `BKPOS_ORDER_DISCOUNT.SUBTOTAL` neu co, neu khong tinh `SUM(TDONHANGCHITIET.THANHTIEN)` | Phai lam tron 0 chu so thap phan. |
| `discount` | `BKPOS_ORDER_DISCOUNT.DISCOUNTAMOUNT` fallback `TDONHANG.TONGGIAMGIA/GIAMGIA/TONGCONGGIAMGIA` | Uu tien snapshot vi dung logic khuyen mai tai thoi diem thanh toan. |
| `total` | `BKPOS_ORDER_DISCOUNT.TOTALAFTERDISCOUNT` fallback `TDONHANG.TONGCONG/TONGTIEN/TIENBAN` | Khong duoc tinh `subtotal - discount` neu da co snapshot/tong trong DB. |
| `paymentMethod` | `BKPOS_PAYMENT` hoac `TDONHANG.PHUONGTHUCTHANHTOAN` | Neu co nhieu dong payment -> `split`; 1 dong -> method cua dong do; khong co `BKPOS_PAYMENT` thi map enum cot cu. |
| `payments` | `BKPOS_PAYMENT.ORDERID = TDONHANG.ID` | Moi dong gom `method`, `amount`, `createdAt`; neu bang chua co thi tao tu fallback method + total. |
| `discountNote` | `BKPOS_ORDER_DISCOUNT.NOTE` | Chi gui noi dung ghi chu, khong them prefix UI. |
| `modifiedAt` | `TDONHANG.TIMEMODIFIED` | Chi dung lam hint/seed; cloud idempotency dung `invoiceVersion`. |

Mapping `TDONHANG.PHUONGTHUCTHANHTOAN` fallback:

```text
1 -> cash
2 -> card
3 -> transfer
null/khac -> other
```

#### 8.3.3 Mapping invoice items

| Cloud field | Firebird source | Ghi chu bat buoc |
|---|---|---|
| `lineId` | `TDONHANGCHITIET.ID` | Fallback resolver: `MACHITIET`, `AUTOID`, `ID`. |
| `productId` | `TDONHANGCHITIET.DMATHANGID` | Join sang `DMATHANG.ID`. |
| `productName` | `TDONHANGCHITIET.TENHANG` | Khong lay ten moi tu `DMATHANG` de tranh doi ten hang lam sai hoa don cu. |
| `productType` | `DMATHANG.LOAISANPHAM` | `0=food`, `1=drink`, `2=other`; neu null/mac dinh -> `drink`. |
| `unitName` | `DMATHANG.DDONVITINHID` -> `DDONVITINH.ID` -> `DDONVITINH.NAME` | Neu khong join duoc thi chuoi rong. |
| `quantity` | `TDONHANGCHITIET.SOLUONG` | So luong nguyen/decimal theo DB, payload dung number. |
| `unitPrice` | `TDONHANGCHITIET.DONGIA` | Gia tai thoi diem ban, khong lay gia moi tu `DMATHANG`. |
| `lineTotal` | `TDONHANGCHITIET.THANHTIEN` fallback `SOLUONG * DONGIA` | Lam tron 0 chu so thap phan. |
| `note` | `TDONHANGCHITIET.NOTE` | Ghi chu bar/bep neu co. |

Chi sync item dang hieu luc:

```text
TDONHANGCHITIET.STATUS = 30
```

Neu DB khach khong co cot `STATUS` o detail thi sync toan bo dong detail cua hoa don.

#### 8.3.4 Bang version cloud local bat buoc

V1 Revenue Cloud phai tao bang rieng de theo doi version sync, vi DB cu co the thieu hoac khong update day du timestamp.

```sql
CREATE TABLE BKPOS_CLOUD_SYNC_VERSION (
    ORDERID      VARCHAR(144) NOT NULL PRIMARY KEY,
    VERSION      BIGINT DEFAULT 1 NOT NULL,
    STATUS       VARCHAR(20) DEFAULT 'paid' NOT NULL,
    UPDATEDAT    TIMESTAMP DEFAULT CURRENT_TIMESTAMP NOT NULL,
    DIRTY        INTEGER DEFAULT 1 NOT NULL,
    LASTSYNCAT   TIMESTAMP,
    LASTERROR    VARCHAR(1000)
);
```

Quy tac:

- Khi sync bat lan dau, hoa don da thanh toan chua co version se duoc seed `VERSION=1`.
- Khi thanh toan thanh cong: upsert version `paid`, tang version neu order da ton tai.
- Khi sua hoa don da thanh toan: tang version va status `edited`.
- Khi huy hoa don: tang version va status `cancelled`.
- `TDONHANG.TIMEMODIFIED` chi duoc dung de phat hien ung vien can seed/recheck, khong la version chinh gui cloud.
- Moi sync payload phai co `invoiceVersion` tu bang nay.

#### 8.3.5 Hoa don huy

Nguon su that:

```text
TDONHANG.STATUS = 0
```

Quy tac:

- Hoa don huy van sync len cloud voi `status=cancelled`.
- Cloud giu invoice/items de audit nhung khong tinh doanh thu.
- Neu huy hoa don ma DB khong co cot `STATUS`, V1 phai chan tinh nang huy online/cloud va log loi cau hinh, khong duoc xoa mem tuy tien.

---

## 9. Cloud schema de xuat

Cloudflare D1 tables:

- `tenants`
- `stores`
- `store_sync_keys`
- `manager_users`
- `manager_sessions`
- `sync_nonces`
- `sync_logs`
- `invoices`
- `invoice_items`
- `daily_revenue_snapshots`
- `audit_logs`

Nguyen tac:

- `tenant_id + store_id + invoice_id + invoice_version` la khoa logic.
- Sync lai cung version thi idempotent, khong nhan doi doanh thu.
- Version moi hon thi cap nhat invoice cloud.
- Hoa don huy khong tinh doanh thu, nhung van giu audit.
- `sync_logs` bat buoc luu duoc `tenant_id`, `store_id`, `event_type`, `message`, `body_sha256`, `request_timestamp`, `request_nonce`, `created_at`.
- Khi signature sai, `message` phai co du thong tin debug toi thieu `ts=<timestamp>;nonce=<nonce>` va `body_sha256` la SHA-256 cua raw body bytes.

### 9.1 Lifecycle bang `daily_revenue_snapshots`

Bang `daily_revenue_snapshots` dung de tang toc dashboard/report, khong phai nguon su that chinh.

Nguon su that chinh:

```text
invoices + invoice_items
```

Quy tac tao/cap nhat snapshot:

- Cloud Worker upsert `daily_revenue_snapshots` ngay sau khi nhan thanh cong `POST /sync/invoices/batch` hoac `POST /sync/invoices/{invoiceId}`.
- Khi batch co nhieu hoa don thuoc nhieu ngay, Worker chi recompute cac ngay bi anh huong theo `tenantId + storeId + businessDate`.
- Cong thuc snapshot phai doc lai tu bang `invoices` sau khi ghi invoice, khong cong don truc tiep tu payload de tranh double count.
- Hoa don `cancelled` khong tinh doanh thu nhung co the tinh vao counter audit neu can.
- Hoa don version moi hon phai lam snapshot ngay do duoc tinh lai.
- Hoa don bi sua doi ngay kinh doanh thi phai recompute ca ngay cu va ngay moi neu `businessDate` thay doi.

Cron reconciliation:

- Worker co the co cron hang ngay luc `00:05` theo timezone cua store de recompute snapshot cua ngay hom truoc va hom nay.
- Cron la lop sua sai/bao tri, khong thay the upsert realtime khi invoice sync den.
- Neu cron loi, report van doc duoc tu `invoices` fallback nhung co the cham hon.

### 9.2 Cleanup bang `sync_nonces`

Bang `sync_nonces` chi de chong replay, khong duoc luu vinh vien.

Quy dinh V1:

- Nonce hieu luc `10` phut.
- Bang phai co toi thieu: `tenant_id`, `store_id`, `nonce`, `created_at`, `expires_at`.
- Unique key: `tenant_id + store_id + nonce`.
- Worker tu dong xoa nonce da het han cu hon `24` gio.
- Cleanup chay theo 2 cach:
  - Opportunistic cleanup: thuc hien nhe khi nhan request sync, co rate-limit de khong chay moi request.
  - Scheduled cleanup: cron it nhat 1 lan/ngay.
- Neu insert nonce bi trung, Worker tra `401 replay_detected`.

---

## 10. Cloud API de xuat

### Sync API

- `POST /sync/heartbeat`
- `POST /sync/invoices/batch`
- `POST /sync/invoices/{invoiceId}`
- `GET /sync/status`

### Manager API

- `POST /auth/login`
- `POST /auth/refresh`
- `POST /auth/logout`
- `GET /reports/today?date=YYYY-MM-DD`
- `GET /reports/range?from=YYYY-MM-DD&to=YYYY-MM-DD`
- `GET /reports/month?month=YYYY-MM`
- `GET /reports/top-products?from=&to=`
- `GET /invoices?from=&to=&status=`
- `GET /invoices/{id}`
- `GET /stores`

### 10.1 Manager API response schema toi thieu

Tat ca Manager API response thanh cong phai tra JSON object co format on dinh. Loi tra `{ "error": "...", "message": "..." }`.

### 10.1.1 Quy tac timezone/date cho Manager reports

Worker chay tren Cloudflare co the dung UTC, nhung bao cao cua quan phai theo ngay kinh doanh dia phuong cua store.

Quy dinh:

- Store co `timezone`, mac dinh V1: `Asia/Ho_Chi_Minh`.
- App Revenue phai gui `date=YYYY-MM-DD` theo ngay dia phuong cua thiet bi/store khi goi `/reports/today`.
- Neu thieu `date`, Worker fallback theo UTC va response phai co `"dateSource": "utc_fallback"` de canh bao co the lech mui gio.
- Neu co `date`, response phai co `"dateSource": "request"`.
- `/reports/month` phai nhan `month=YYYY-MM`; App Revenue tinh theo timezone store/thiet bi.
- `/reports/range` phai nhan `from` va `to` theo ngay dia phuong, khong duoc suy tu UTC "hom nay".
- Cloud query/report dung `businessDate` cua invoice/snapshot, khong dung thoi diem Worker nhan request.

#### 10.1.2 `GET /reports/today?date=YYYY-MM-DD`

```json
{
  "storeId": "STORE-001",
  "timezone": "Asia/Ho_Chi_Minh",
  "dateSource": "request",
  "businessDate": "2026-05-20",
  "lastSyncAt": "2026-05-20T08:35:00+07:00",
  "summary": {
    "revenue": 900000,
    "invoiceCount": 10,
    "cancelledInvoiceCount": 1,
    "averageInvoiceValue": 90000,
    "cashAmount": 500000,
    "transferAmount": 300000,
    "cardAmount": 100000,
    "otherAmount": 0
  },
  "revenue7Days": [
    { "date": "2026-05-14", "revenue": 1200000, "invoiceCount": 18 }
  ],
  "paymentBreakdown": [
    { "method": "cash", "amount": 500000 },
    { "method": "transfer", "amount": 300000 },
    { "method": "card", "amount": 100000 },
    { "method": "other", "amount": 0 }
  ]
}
```

Quy tac payment totals:

- `summary.cashAmount`, `summary.transferAmount`, `summary.cardAmount`, `summary.otherAmount` phai tinh tu tong `payment_line.amount` theo `payment_line.method`.
- `paymentBreakdown` cung dung nguon tinh tu `payment_line.amount`.
- Khong duoc tinh bat ky so tien thanh toan nao trong summary tu invoice-level `paymentMethod`.
- Tong `paymentBreakdown.amount` cua cac method phai bang tong doanh thu cua hoa don da thanh toan trong report, sai so lam tron toi da `1`.

#### 10.1.3 `GET /reports/top-products?from=&to=`

```json
{
  "from": "2026-05-01",
  "to": "2026-05-20",
  "items": [
    {
      "productId": "B52",
      "productName": "B52",
      "productType": "drink",
      "quantity": 20,
      "revenue": 600000
    }
  ]
}
```

#### 10.1.4 `GET /reports/range?from=&to=`

```json
{
  "from": "2026-05-01",
  "to": "2026-05-20",
  "summary": {
    "revenue": 15000000,
    "invoiceCount": 180,
    "cancelledInvoiceCount": 5,
    "averageInvoiceValue": 83333
  },
  "daily": [
    { "date": "2026-05-01", "revenue": 900000, "invoiceCount": 12 }
  ],
  "paymentBreakdown": [
    { "method": "cash", "amount": 8000000 },
    { "method": "transfer", "amount": 6000000 },
    { "method": "card", "amount": 1000000 },
    { "method": "other", "amount": 0 }
  ]
}
```

#### 10.1.5 `GET /reports/month?month=YYYY-MM`

```json
{
  "month": "2026-05",
  "timezone": "Asia/Ho_Chi_Minh",
  "summary": {
    "revenue": 15000000,
    "invoiceCount": 180,
    "cancelledInvoiceCount": 5,
    "averageInvoiceValue": 83333
  },
  "daily": [
    { "date": "2026-05-01", "revenue": 900000, "invoiceCount": 12 }
  ],
  "paymentBreakdown": [
    { "method": "cash", "amount": 8000000 },
    { "method": "transfer", "amount": 6000000 },
    { "method": "card", "amount": 1000000 },
    { "method": "other", "amount": 0 }
  ]
}
```

#### 10.1.6 `GET /invoices?from=&to=&status=&page=&pageSize=`

Pagination bat buoc.

```json
{
  "page": 1,
  "pageSize": 50,
  "totalItems": 125,
  "items": [
    {
      "invoiceId": "ORDER-001",
      "invoiceVersion": 2,
      "status": "paid",
      "businessDate": "2026-05-20",
      "tableName": "Ban A1",
      "cashier": "Administrator",
      "paidAt": "2026-05-20T08:30:00+07:00",
      "subtotal": 1000000,
      "discount": 100000,
      "total": 900000,
      "paymentMethod": "split"
    }
  ]
}
```

#### 10.1.7 `GET /invoices/{id}`

Tra day du invoice header, `payments` va `items` dung schema muc 8.

---

## 11. Module sync tai quan

### 11.1 Vi tri module sync da chot

Module sync V1 bat buoc nam trong `BKPos.Mobile.Api` (BKPos Mobile Server API).

Khong chon service Windows rieng trong V1 va khong dat trong BKPos Desktop.

Ly do:

- Mobile Server API thuong chay lien tuc tai quan de phuc vu mobile order.
- Neu sync dat trong Desktop thi desktop dong/mo se lam sync khong lien tuc.
- Service rieng lam tang do phuc tap cai dat/bao tri trong V1.
- Dong goi hien tai da co Mobile Server API, them module sync vao day it rui ro nhat.

Yeu cau:

- Khong sua luong order/thanh toan/in an hien tai.
- Chi doc du lieu da hoan tat de sync.
- Co hang doi sync local.
- Retry co backoff.
- Khong gui trung invoice.
- Co log file rieng.
- Co trang thai sync gan nhat.
- Mat Internet van ban hang binh thuong.
- Co Internet lai thi sync bu.

Bang local can them khi bat sync:

- `BKPOS_CLOUD_SYNC_QUEUE`
- `BKPOS_CLOUD_SYNC_VERSION`
- `BKPOS_CLOUD_SYNC_STATE`
- `BKPOS_CLOUD_SYNC_LOG`

Neu DB khach chua co bang nay:

- Tu tao mem khi bat sync.
- Neu tao that bai thi sync disabled va log loi.
- Khong duoc chan ung dung POS.

---

## 12. App Revenue Android/iPhone

Nen tang de xuat:

- .NET MAUI Android/iOS de tai su dung kinh nghiem .NET hien co.
- iOS V1 giai doan dau build qua GitHub Actions macOS va upload TestFlight; khong bat buoc co Mac local.
- Xcode Cloud la phuong an du phong/sau neu can dua pipeline vao he sinh thai App Store Connect.

### 12.1 Dieu kien iOS V1 voi GitHub Actions macOS/TestFlight

Chu du an da co Apple Developer account. V1 iOS duoc xem la kha thi neu Sprint 0 pass pipeline GitHub Actions macOS va TestFlight.

Dieu kien bat buoc truoc khi dev UI iOS day du:

- Apple Developer Program membership dang active.
- Co app record tren App Store Connect hoac quyen tao app record.
- Co bundle identifier duy nhat cho BKPos Revenue App.
- Source code nam tren GitHub repository.
- Repo co workflow `.github/workflows/ios-revenue-testflight.yml`.
- GitHub Actions macOS build duoc `.ipa` tu `BKPos.Revenue.App/BKPos.Revenue.App.csproj`.
- Signing certificate/provisioning profile/App Store Connect API key duoc luu trong GitHub Actions Secrets, khong commit vao source.
- Workflow upload duoc build len TestFlight.
- It nhat 1 thiet bi iPhone cai duoc qua TestFlight va dang nhap/load dashboard thanh cong.

Neu Sprint 0 iOS pipeline khong pass:

- Khong duoc hua giao iOS trong V1.
- Android Revenue App van co the lam truoc neu chu du an chot.
- Spec phai cap nhat trang thai iOS thanh `defer` truoc khi code tiep.

Quy tac khi repo dang public:

- Khong commit `.p12`, `.mobileprovision`, `.p8`, `ADMIN_SECRET`, `SyncKey` hoac secret cloud.
- Workflow khong chay tren `pull_request`.
- Chi maintainer duoc push `main` hoac chay manual upload TestFlight.
- Sau khi build on dinh co the chuyen repo ve private; can kiem tra lai GitHub Actions billing/permission.
- Neu tung lo secret khi repo public thi phai rotate secret.

Tai lieu trien khai chi tiet:

```text
Docs/BKPos-Revenue-iOS-TestFlight-GitHubActions.md
```

Man hinh V1:

- Login.
- Chon cua hang neu tai khoan co nhieu store.
- Dashboard hom nay.
- Bao cao theo ngay/thang/khoang ngay.
- Top mon ban chay.
- Danh sach hoa don.
- Chi tiet hoa don.
- Trang thai dong bo.
- Cai dat tai khoan/dang xuat.

UI yeu cau:

- Native mobile, touch-first.
- Navy/blue/red theo BKPos.
- Bieu do ro rang.
- Refresh nhanh.
- Hien thi tien dong nhat: `100.000đ`.

### 12.2 App Revenue khi offline

App Revenue chi la app xem bao cao cloud, nen khi mat mang phai co UX ro rang.

Quy dinh V1:

- App cache lan thanh cong gan nhat cho dashboard, report khoang ngay gan nhat, top mon va danh sach hoa don gan nhat.
- Khi khong co mang, khong hien man trang.
- Hien banner: `Dang offline - dang hien thi du lieu da luu luc <time>`.
- Tat ca du lieu offline la read-only.
- Nut refresh khi offline hien popup/toast: `Khong co ket noi Internet`.
- Neu chua tung login/cache thanh cong tren thiet bi, man hinh phai hien thong bao `Chua co du lieu offline` va nut thu lai.
- App khong duoc tu dong logout chi vi mat mang.
- Neu refresh token het han trong khi offline, app van cho xem cache nhung yeu cau dang nhap lai khi co mang.

Pham vi cache offline V1:

- Cache response thanh cong gan nhat cua `GET /reports/today?date=YYYY-MM-DD` theo `storeId + date`.
- Cache toi da `10` response gan nhat cua `GET /reports/range?from=&to=` theo `storeId + from + to`.
- Cache response thanh cong gan nhat cua `GET /reports/top-products?from=&to=` theo `storeId + from + to`.
- Cache trang 1 cua `GET /invoices`.
- Cache toi da `100` invoice detail gan nhat cua `GET /invoices/{id}` theo `storeId + userId`, dung LRU de tu dong loai detail cu nhat khi vuot gioi han.
- Cache metadata `lastSyncAt`, `cachedAt`, `storeId`, `tenantId` de UI hien thi ro du lieu cu luc nao.
- TTL cache hien thi: `7` ngay. Qua 7 ngay thi van co the giu file local, nhung UI phai hien canh bao `Du lieu offline da cu`.
- Logout phai xoa cache cua user hien tai tren thiet bi.
- Token/refresh token khong tinh la cache report va phai luu bang secure storage cua nen tang.

### 12.3 Bieu do V1 bat buoc

De tranh scope creep, V1 chi can cac bieu do sau:

- Bar chart: doanh thu 7 ngay gan nhat.
- Line chart: doanh thu theo ngay trong thang dang chon.
- Pie/donut chart: ty trong phuong thuc thanh toan theo payment-line `cash`, `transfer`, `card`, `other`.

Ngoai 3 loai tren la V2, khong dua vao nghiem thu V1 neu chua duoc chot rieng.

Logic pie/donut payment:

- Khong tinh theo invoice-level `paymentMethod`.
- Phai tinh theo tong `payments[].amount` gom cac method: `cash`, `transfer`, `card`, `other`.
- Hoa don invoice-level `paymentMethod = split` van dong gop vao tung lat pie theo tung payment-line thuc te.
- Pie/donut khong co lat `split`.
- Hoa don `cancelled` khong tinh vao pie/donut doanh thu.

---

## 13. Checklist truoc khi code

### 13.1 Phan tich

- [x] Xac dinh bang/cot Firebird dung de doc hoa don da thanh toan: xem muc 8.3.
- [x] Xac dinh cach lay hoa don bi huy: `TDONHANG.STATUS = 0`.
- [x] Xac dinh cach lay hoa don da sua sau thanh toan: `BKPOS_CLOUD_SYNC_VERSION.VERSION` tang khi edit.
- [x] Xac dinh version invoice: V1 tao `BKPOS_CLOUD_SYNC_VERSION`, khong chi dua vao timestamp cu.
- [x] Xac dinh phuong thuc thanh toan tien mat/chuyen khoan/split: `BKPOS_PAYMENT` uu tien, fallback `TDONHANG.PHUONGTHUCTHANHTOAN`.
- [x] Xac dinh du lieu giam gia va ghi chu khuyen mai: `BKPOS_ORDER_DISCOUNT`.
- [ ] Sprint 0 iOS: xac nhan GitHub Actions macOS build/upload TestFlight duoc app mau/minimal.
- [x] Sprint 0 cloud: Worker verify HMAC canonical voi body bytes dung nhu muc 5.3.
- [x] Xac dinh lifecycle `daily_revenue_snapshots`: Worker upsert khi invoice sync den, cron chi reconcile.
- [x] Xac dinh cleanup `sync_nonces`: TTL 10 phut, cleanup nonce cu hon 24 gio.
- [x] Xac dinh thoi han token App Revenue: access 60 phut, refresh 30 ngay.
- [x] Xac dinh UX offline App Revenue: hien cache read-only, khong man trang.
- [x] Xac dinh chart V1: bar 7 ngay, line theo thang, pie payment method.
- [x] Xac dinh `CloudSync.IntervalSeconds` mac dinh: 300 giay.
- [x] Chuan hoa enum payment: invoice co `split`, payment-line khong co `split`, khong dung `card/other`.
- [x] Chuan hoa pie/donut payment: tinh theo `payments[].amount`, khong tinh theo invoice-level `paymentMethod`.
- [x] Bo sung Manager API response schema toi thieu cho `today`, `top-products`, `range`, `invoices`.
- [x] Xac dinh pham vi cache offline App Revenue theo endpoint va TTL 7 ngay.
- [x] Xac dinh report timezone/date: App gui `date/month`, Worker dung `businessDate`, fallback UTC phai canh bao.
- [x] Bo sung response schema cho `GET /reports/month?month=YYYY-MM`.
- [x] Xac dinh summary payment totals phai tinh tu payment-line amount, khong tu invoice-level `paymentMethod`.
- [x] Xac dinh `/reports/range` phai co `paymentBreakdown` de dong nhat dashboard/report.
- [x] Xac dinh cache chi tiet hoa don offline toi da 100 detail theo `storeId + userId`, dung LRU.

### 13.2 Cloud

- [x] Tao Worker rieng cho Revenue Cloud.
- [ ] Tao D1 database.
- [x] Viet migrations.
- [x] Viet auth manager.
- [x] Viet sync HMAC validation.
- [x] Viet reports API.
- [x] Viet audit logs.
- [x] Hash mat khau manager bang PBKDF2-HMAC-SHA256 100000 iterations, khong dung SHA-256 mot lan.
- [x] `sync_logs` luu `request_timestamp/request_nonce/body_sha256` khi signature sai.
- [x] Viet upsert/recompute `daily_revenue_snapshots`.
- [x] Viet cleanup `sync_nonces` theo TTL/cron.
- [x] Viet token/session TTL va revoke server-side.
- [x] Viet response schema dung muc 10.1 va test contract JSON.
- [x] Viet report date/month theo timezone/store businessDate, khong dung UTC mac dinh neu client da gui date.
- [x] Viet summary payment totals va paymentBreakdown cung mot nguon `payment_line.amount`.

### 13.3 KeyGen/license

- [x] Them feature `RevenueCloud`.
- [x] Them Store ID/Sync Key.
- [x] Them thu hoi Revenue Cloud.
- [x] Khong lam hong key Desktop/Mobile hien co.
- [x] Cap nhat UI quan ly license ro rang.

### 13.4 Local sync

- [x] Them setting `CloudSync.Enabled` mac dinh false.
- [x] Them UI cau hinh sync.
- [x] Them API cau hinh sync co auth: `GET/POST /cloud-sync/config`.
- [x] Them queue local.
- [x] Them job sync nen.
- [x] Them retry va log.
- [ ] Mat mang khong anh huong ban hang.

### 13.5 App Revenue

- [x] Login.
- [x] Dashboard.
- [x] Bao cao khoang ngay.
- [x] Hoa don.
- [x] Top mon.
- [x] Trang thai sync.
- [x] Cache offline va banner offline.
- [x] Cache offline dung pham vi muc 12.2 va xoa cache khi logout.
- [x] Bar chart 7 ngay gan nhat.
- [x] Line chart theo thang.
- [x] Pie/donut chart phuong thuc thanh toan.
- [x] Pie/donut tinh theo payment-line amount, khong tao lat `split`.
- [x] Logout.

---

## 14. Tieu chi nghiem thu V1

- [ ] Khach khong bat sync: POS hoat dong nhu cu.
- [ ] Khach bat sync: doanh thu hom nay len cloud dung.
- [ ] Mat mang 30 phut: ban hang khong loi, co mang sync bu dung.
- [ ] Hoa don huy: cloud cap nhat va khong tinh doanh thu.
- [ ] Hoa don sua sau thanh toan: cloud cap nhat version moi, tong tien dung.
- [ ] Giam gia: cloud hien dung subtotal, discount, total.
- [ ] App Revenue Android xem dung.
- [ ] App Revenue iPhone xem dung.
- [ ] Tai khoan bi thu hoi khong dang nhap duoc.
- [ ] Store bi tat Revenue Cloud khong sync duoc.
- [ ] Khong co secret admin trong app Revenue.
- [ ] Snapshot doanh thu ngay khong nhan doi khi sync lai cung invoice version.
- [ ] Nonce het han duoc cleanup, bang `sync_nonces` khong phinh vo han.
- [ ] Access token het han sau 60 phut, refresh token het han sau 30 ngay.
- [ ] Logout/revoke lam session cu khong dung duoc.
- [ ] App Revenue mat mang van hien cache cu co thong bao offline.
- [ ] Bieu do V1 dung 3 loai da chot: bar 7 ngay, line theo thang, pie/donut payment.
- [ ] Pie/donut payment khong co lat `split`, hoa don split duoc tach theo tung dong payment.
- [ ] Manager API response dung schema muc 10.1, co pagination cho invoices.
- [ ] `/reports/today?date=` tra dung ngay dia phuong, khong lech khi UTC khac ngay Viet Nam.
- [ ] `/reports/month?month=` tra dung schema va daily list cua thang.
- [ ] `summary.cashAmount/transferAmount/cardAmount/otherAmount` khop voi `paymentBreakdown` va deu tinh tu payment lines.
- [ ] `/reports/range?from=&to=` co `paymentBreakdown` tinh tu payment-line amount.
- [ ] App Revenue cache toi da 100 invoice detail gan nhat theo `storeId + userId`, logout xoa cache user hien tai.

---

## 15. Quy tac cam ket cho Dev

- Khong sua logic order/thanh toan/in an de phuc vu sync neu chua duoc chot.
- Khong mo Firebird/PC quan ra Internet.
- Khong bat sync mac dinh.
- Khong lam hong license cu.
- Khong de loi cloud chan ban hang.
- Khong luu secret sync trong app Revenue.
- Khong dong goi APK Revenue vao installer Desktop neu chua co yeu cau.

---

## 16. Xac nhan

Chu du an xac nhan cac diem sau truoc khi code:

- [ ] Dong y kien truc cloud sync.
- [ ] Dong y mac dinh sync OFF.
- [ ] Dong y mo rong license bang feature `RevenueCloud`.
- [ ] Dong y app Revenue chi xem bao cao trong V1.
- [ ] Dong y khong anh huong khach cu neu khong bat sync.
- [ ] Dong y Cloudflare Worker + D1 la backend cloud V1.
- [ ] Dong y module sync V1 nam trong `BKPos.Mobile.Api`.
- [ ] Dong y tao bang `BKPOS_CLOUD_SYNC_VERSION` de quan ly invoice version.
- [ ] Dong y iOS V1 chi bat dau sau khi Sprint 0 GitHub Actions macOS/TestFlight pipeline pass.
- [ ] Dong y Worker upsert `daily_revenue_snapshots` khi invoice sync den va cron chi reconcile.
- [ ] Dong y `sync_nonces` TTL 10 phut, cleanup nonce cu hon 24 gio.
- [ ] Dong y token App Revenue: access 60 phut, refresh 30 ngay, logout xoa session server-side.
- [ ] Dong y App Revenue offline chi hien cache read-only.
- [ ] Dong y chart V1 gom bar 7 ngay, line theo thang, pie/donut payment theo payment-line amount.
- [ ] Dong y `CloudSync.IntervalSeconds` mac dinh 300 giay khi bat sync.
- [ ] Dong y enum payment chuan: invoice `cash|transfer|card|split|other`, payment-line `cash|transfer|card|other`.
- [ ] Dong y Manager API response schema toi thieu theo muc 10.1.
- [ ] Dong y App Revenue gui `date` cho `/reports/today` va `month` cho `/reports/month` theo ngay gio dia phuong/store.
- [ ] Dong y moi so tien payment trong summary/report tinh tu payment-line amount, khong tu invoice-level `paymentMethod`.
- [ ] Dong y `/reports/range` co `paymentBreakdown` de cac man hinh thong ke dung chung mot contract.
- [ ] Dong y cache chi tiet hoa don offline bi gioi han 100 detail gan nhat theo `storeId + userId`.


