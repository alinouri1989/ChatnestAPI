# ChatGPT Refactor Notes

## 2026-07-11 - Database-backed application version policy

- Added SQL entities, a migration, and seed data for Android, iOS, and PWA policies and ordered release notes.
- Changed `GET /api/app-version` from configuration-backed evaluation to a database query.
- Added cross-platform version-policy documentation.

See UI changelog for frontend details.

## 2026-07-09 - Pinning and Kavenegar OTP

- Added per-user pinned chat metadata with `PinnedForJson` on chats and an EF migration: `20260709071747_AddPinnedChats`.
- Added REST endpoints:
  - `POST api/Chat/{chatId}/Pin?chatType=`
  - `DELETE api/Chat/{chatId}/Pin?chatType=`
- Added Kavenegar package support for mobile OTP login using `KavenegarApi.VerifyLookup`.
- Added Melipayamak OTP sending through the shared JSON endpoint:
  `POST https://console.melipayamak.com/api/send/shared/{SharedToken}`
- Kept the previous Melipayamak `BaseServiceNumber` API as an alternate mode with `Melipayamak:SendMode = BaseServiceNumber`.
- Added configurable SMS provider selection with `Sms:DefaultProvider`; default is `Melipayamak`.
- Added auth endpoints:
  - `POST api/Auth/RequestLoginOtp`
  - `POST api/Auth/VerifyLoginOtp`
- Added SMS config keys:
  - `Sms:DefaultProvider`
  - `Kavenegar:ApiKey`, `Kavenegar:OtpTemplate`, `Kavenegar:OtpExpiryMinutes`
  - `Melipayamak:SendMode`, `Melipayamak:SharedToken`, `Melipayamak:Username`, `Melipayamak:Password`, `Melipayamak:BodyId`
- Registration now stores normalized mobile on `PhoneNumber` and `MobileNo` when provided.
- Verified the existing LiveKit/SignalR call code builds without changes.

Verification:
- `dotnet build chatnest.api\ChatNest.sln`
- `dotnet test chatnest.api\ChatNest.Tests\ChatNest.Tests.csproj --no-build`

## API changes
- Added `ChatController` with REST endpoints:
  - `GET api/Chat/Initial?skip=&take=`
  - `GET api/Chat/Total`
  - `GET api/Chat/{chatId}/Messages?skip=&take=`
  - `GET api/Chat/{chatId}/MessagesByDay?beforeUtc=`
- Added `CallController` with REST endpoints:
  - `GET api/Call?skip=&take=`
  - `GET api/Call/Total`
- Added paginated call methods to `ICallRepository`, `CallRepository`, `ICallService`, and `CallService`.
- Added `ChatNest.Tests` xUnit test project and solution entries.

## Verification note
The API build and test suite were verified on 2026-07-09 with .NET 10.
