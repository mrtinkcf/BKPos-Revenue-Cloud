# BKPos Revenue App - iOS TestFlight via GitHub Actions macOS

Tai lieu nay dung cho giai doan dau: build iOS bang GitHub Actions macOS va phat hanh TestFlight. Khi app on dinh moi submit App Store.

## 1. Thu muc build

Project iOS nam tai:

```text
BKPos.Revenue.App/BKPos.Revenue.App.csproj
```

Workflow GitHub Actions:

```text
.github/workflows/ios-revenue-testflight.yml
```

## 2. Yeu cau Apple

Can co:

- Apple Developer Program dang active.
- App record tren App Store Connect.
- Bundle ID: `vn.baokhang.bkpos.revenue`.
- Apple Distribution certificate dang `.p12`.
- App Store provisioning profile dang `.mobileprovision` cho bundle ID tren.
- App Store Connect API Key dang `.p8` de upload TestFlight.

## 3. GitHub Secrets bat buoc

Vao GitHub repo -> Settings -> Secrets and variables -> Actions -> New repository secret.

### Signing iOS

| Secret | Noi dung |
|---|---|
| `IOS_CERTIFICATE_P12_BASE64` | File Apple Distribution `.p12` ma hoa base64 |
| `IOS_CERTIFICATE_PASSWORD` | Mat khau file `.p12` |
| `IOS_PROVISIONING_PROFILE_BASE64` | File `.mobileprovision` ma hoa base64 |
| `IOS_CODESIGN_KEY` | Ten certificate, vi du `Apple Distribution: Bao Khang Laptop (TEAMID)` |
| `IOS_CODESIGN_PROVISION` | Ten provisioning profile trong Apple Developer |
| `IOS_KEYCHAIN_PASSWORD` | Mat khau tam cho keychain trong runner |

### Upload TestFlight

| Secret | Noi dung |
|---|---|
| `APPSTORE_API_KEY_ID` | Key ID cua App Store Connect API key |
| `APPSTORE_ISSUER_ID` | Issuer ID trong App Store Connect |
| `APPSTORE_API_KEY_BASE64` | File `AuthKey_XXXX.p8` ma hoa base64 |

## 4. Cach tao chuoi base64 tren Windows

Chay PowerShell:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\ios_distribution.p12")) | Set-Clipboard
```

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\profile.mobileprovision")) | Set-Clipboard
```

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\AuthKey_XXXX.p8")) | Set-Clipboard
```

Dan gia tri clipboard vao GitHub Secret tuong ung.

## 5. Cach chay build

### Build thu khong upload TestFlight

GitHub -> Actions -> `Build iOS Revenue App to TestFlight` -> Run workflow:

```text
upload_testflight = false
```

Ket qua: GitHub Actions tao artifact `BKPos.Revenue.App-iOS-IPA`.

### Build va upload TestFlight

GitHub -> Actions -> Run workflow:

```text
upload_testflight = true
```

Workflow se:

1. Checkout source.
2. Cai .NET SDK 10.
3. Cai MAUI iOS workload.
4. Restore project.
5. Import certificate/profile vao macOS keychain tam.
6. Build `.ipa`.
7. Upload artifact.
8. Upload TestFlight bang App Store Connect API.

## 6. Quy tac an toan khi repo public

- Khong commit file `.p12`, `.mobileprovision`, `.p8`.
- Khong commit `ADMIN_SECRET`, `SyncKey`, App Store API key.
- Workflow khong chay tren `pull_request` de tranh secret bi dung sai.
- Chi maintainer moi duoc push vao `main` va chay workflow upload TestFlight.
- Sau khi chuyen repo ve private, kiem tra lai GitHub Actions minutes/billing.
- Neu tung commit nham secret khi repo public, phai rotate ngay secret do.

## 7. Khach test qua TestFlight

- Noi bo: them tester trong App Store Connect.
- Ben ngoai: moi bang email hoac public link TestFlight.
- Ban external thuong can Apple Beta App Review truoc khi tester ben ngoai cai duoc.
- Khi co build moi, tester nhan thong bao update trong app TestFlight.

## 8. Tieu chi Sprint 0 iOS pass

Sprint 0 iOS chi duoc xem la pass khi:

- GitHub Actions build `.ipa` thanh cong.
- `.ipa` upload duoc len TestFlight.
- It nhat 1 thiet bi iPhone cai duoc qua TestFlight.
- Login duoc vao App Revenue va load dashboard tu Worker test.

Neu mot trong cac diem tren fail thi khong duoc danh dau iOS V1 hoan tat.
