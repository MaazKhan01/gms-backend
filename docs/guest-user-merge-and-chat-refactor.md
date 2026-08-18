# Guest/User merge, notification centralization, Firebase completion, driver<->guest chat

Migration: `DomainPersistence/Migrations/20260730125545_MergeGuestIntoUserAndUnifyNotifications.cs`

## 1. Architecture decision

`Guest` becomes a 1:1 profile extension of `User` (`Guest.UserId`), mirroring the
existing `DriverProfile` <-> `User` pattern exactly. A new `Role` (`guest`,
`PortalAccess=false`, no permissions) is auto-provisioned. `Guest` itself is
**not removed** — it still holds every event-scoped field (`EventId`,
`GuestType`, `Tier`, `PreferencesJson`, `ArrivalDate`/`DepartureDate`, etc.).
Only the concerns that were duplicated purely to work around "Guest isn't a
User" — chat, notifications, devices — move onto the shared `User`-based
tables.

Guest identity for the **existing** OTP/VIP-app auth flow still resolves to
`Guest.Id` via `ICurrentGuest`, and every guest-scoped route
(`/support-chat/my/*`, `/notifications/guest/*`, `/vip-app/*`, seating,
invitations, etc.) is untouched. What changed is where that id comes from: the
guest JWT now carries **only** the linked `User.Id` (`sub` and
`ClaimTypes.NameIdentifier`) plus `role=="guest"` — the old `Guest.Id` claim and
its `GuestClaims` constant are gone. `CurrentGuest` derives `Guest.Id` with one
cached `Guests.UserId` lookup per request. This is what lets `ICurrentUser`,
`Clients.User(...)` SignalR targeting, and `AuditInterceptor`'s
`CreatedBy`/`UpdatedBy` stamping resolve correctly for a guest token the same
way they already do for a staff token, without any client-side change.

> **Superseded in part — see [guest-schema-eventguest.md](guest-schema-eventguest.md).**
> The paragraph below described the state when `Guest` was one row *per event*.
> It no longer is: `Guest` is the master person record, `EventGuest` is the
> per-event participation, and `Guests.Email` is now **required and unique among
> active rows** — it is also the linked `User`'s `Email` *and* `UserName`. The
> 1:1 `Guest`/`User` extension-table shape and everything below about the JWT,
> `CurrentGuest` and unified notifications is unchanged.

Why not fully absorb `Guest` into `Users` (single table)? At the time, `Users.Email`/
`Users.UserName` were unique (filtered) while `Guests.Email` was not — the same
person then had one `Guest` row per event, all sharing an email. Merging by
shared key would have forced either dropping that flexibility or a much larger,
riskier schema change. The extension-table approach reuses an already-proven
pattern in this codebase and needed no changes to guest event/registration
behavior. (The `EventGuest` split has since removed the duplicate-email case, so
the two uniqueness rules now agree rather than conflict.)

## 2. Database changes

**Migration `MergeGuestIntoUserAndUnifyNotifications`** (one migration, ordered
in phases — see its file-level comment for why order matters):

- `Guests.UserId` (int, NOT NULL, unique FK -> `Users.Id`, cascade) — added
  nullable, backfilled, then locked to NOT NULL.
- **Data backfill**: one `User` row created for every existing `Guest`
  (`RoleId` -> `guest` role, created defensively in the same migration if the
  app-startup seeder hasn't run yet; `FirstName`/`LastName` copied,
  `Email`/`UserName`/`PasswordHash` left `NULL`).
- New `Devices` table (replaces `GuestDevices`) — `UserId` FK -> `Users`
  (cascade), unique `Token`, plus every field from Task 4B:
  `DeviceIdentifier`, `DeviceModel`, `OsVersion`, `AppVersion`,
  `NotificationsEnabled`, `IsActive`, `LastActiveAt`, `TokenUpdatedAt`.
- **Data copy**: `GuestDevices` rows -> `Devices` (re-keyed via
  `Guests.UserId`), then `GuestDevices` table dropped.
- **Data copy**: `GuestNotifications` rows -> `Notifications` (re-keyed via
  `Guests.UserId`), then `GuestNotifications` table dropped.
- `SupportMessages.GuestId` / `SupportConversations.GuestId` renamed to
  `UserId` (FK -> `Users`, cascade) — **values remapped**, not just the column
  name, from "the old `Guest.Id`" to "the guest's linked `User.Id`".
- `SupportConversations` gains `Type` (`AdminSupport` default / `DriverGuest`)
  and nullable `OtherUserId` (FK -> `Users`, restrict — the driver's `User.Id`
  for a `DriverGuest` thread).
- Old unique index `SupportConversations.GuestId` replaced with **two**
  filtered unique indexes: `(UserId, Type) WHERE OtherUserId IS NULL` (one
  admin-support thread per guest) and `(UserId, Type, OtherUserId) WHERE
  OtherUserId IS NOT NULL` (one thread per guest/driver pair). A single
  composite index would not have worked here — EF Core's SqlServer provider
  automatically filters a unique index to exclude NULLs whenever it covers a
  nullable column, which would have silently stopped enforcing "one
  admin-support thread per guest" (`OtherUserId` is always null there).

**Rollback (`Down()`)**: schema is fully reversible. Data is reversible for
the Guest<->User link and the `AdminSupport` conversation re-key (remapped
back before the rename). It is **not** reversible for the
`GuestNotification`/`GuestDevice` consolidation (can't tell which rows in the
now-shared tables originated pre-migration) and **only safe to run before any
`DriverGuest` conversation exists** (the old unique-per-guest index can't be
recreated once a guest has more than one conversation row). See the
migration's `Down()` comment.

## 3. Entity changes

- `DomainPersistence/Entities/Guest.cs` — `+UserId`, `+User` nav.
- `DomainPersistence/Entities/User.cs` — `+GuestProfile` nav, `+Devices` collection.
- `DomainPersistence/Entities/Device.cs` — **new**, replaces `GuestDevice.cs` (deleted).
- `DomainPersistence/Entities/GuestNotification.cs` — **deleted**, folded into `Notification`.
- `DomainPersistence/Entities/SupportConversation.cs` — `GuestId`/`Guest` ->
  `UserId`/`User`; `+Type`, `+OtherUserId`, `+OtherUser`.
- `DomainPersistence/Entities/SupportMessage.cs` — `GuestId`/`Guest` -> `UserId`/`User`.
- `Core/Constants/RoleDefinitions.cs` — `RoleDef` gained an optional
  `PortalAccess` flag (default `true`, so every existing call site is
  unaffected); `driver` and the new `guest` role both pass `false`.
  `DataSeeder` now syncs `PortalAccess` from `RoleDefinitions` the same way it
  already syncs `Name`/`Description` — this also fixes a pre-existing gap
  where the `driver` role's `PortalAccess` was never set by the seeder.
- `Core/Constants/SupportChatTypes.cs` — **new** (`AdminSupport`, `DriverGuest`).

## 4. Service / repository changes

- `Core/Interfaces/Repositories/IUnitOfWork.cs` + `Infrastructure/Database/Repositories/UnitOfWork.cs`
  — `GuestDevices`/`GuestNotifications` repos removed, `Devices` repo added.
- `Infrastructure/Services/Guest.cs` (`GuestService`) — `CreateGuestAsync` now
  also creates the linked `User` (`CreateLinkedUserAsync`); `UpdateGuestAsync`
  keeps the linked `User`'s `FirstName`/`LastName` in sync. CSV import goes
  through `CreateGuestAsync`, so it's covered too.
- `Infrastructure/Services/VipAppService.cs` — `BuildAccessToken`: `sub` and
  `ClaimTypes.NameIdentifier` are both `guest.UserId` (previously `guest.Id`);
  the `"Id"` (`Guest.Id`) claim is removed. Both must match — the JWT handler
  maps inbound `sub` onto `NameIdentifier`, so a differing `sub` wins the
  `FindFirst` and resolves the wrong identity. The refresh token's `sub` is
  still `Guest.Id`; it's server-validated against a stored `jti` and never read
  as an identity.
- `Infrastructure/Services/CurrentGuest.cs` — now takes `IUnitOfWork` and
  resolves `Guest.Id` from `Guests.UserId` (one cached query per request),
  gated on `role=="guest"`. `Core/Constants/GuestClaims.cs` deleted.
- `Core/Helpers/RealTimeHubService.cs` — same lookup on connect, because
  senders still target `guest:{Guest.Id}` groups.
- `Infrastructure/Services/SupportChatService.cs` — full rewrite onto the
  `UserId`/`Type`/`OtherUserId` model. Every existing public method keeps its
  exact signature and behavior for `AdminSupport` (guestId in, guest PublicId
  in for admin-initiated); internally everything now filters
  `Type == AdminSupport` so a `DriverGuest` thread never leaks into an admin
  inbox or a guest's "my conversation" view. Added
  `SendDriverGuestMessageAsync` (Task 6, see §6).
- `Infrastructure/Notification/NotificationManagerService.cs` —
  `SendToGuestAsync`/`SendToGuestsAsync`/`BroadcastToAllGuestsAsync` keep their
  `guestId`-based signatures (every caller is unaffected) but now resolve to
  the guest's `UserId` and reuse the exact same persist-then-push path as
  every other `User` recipient — no more parallel `GuestNotification` code path.
- `Infrastructure/Notification/ManualNotificationProvider.cs` — simplified to
  a single `Clients.User(...)` path (the `guest:{id}` SignalR-group branch is
  gone; every recipient is a `User` now).
- `Infrastructure/Notification/FirebaseNotificationProvider.cs` — real FCM
  implementation (see §5), fans out over `Devices` filtered by `UserId`
  instead of `GuestDevices` filtered by `GuestId`.
- `Infrastructure/Notification/NotificationCleanupJob.cs` — one purge sweep
  over `Notifications` (the `GuestNotifications` branch is gone).
- `Infrastructure/Notification/NotificationService.cs` — guest-facing methods
  (`GetGuestNotificationsAsync`, `MarkGuestNotificationReadAsync`, etc.) now
  resolve the guest's `UserId` and read/write `Notifications`, not
  `GuestNotifications`. `RegisterGuestDeviceAsync` is kept (existing VIP app
  route) as a thin wrapper over a new generic `RegisterDeviceAsync`/
  `DeregisterDeviceAsync` pair usable by any authenticated `User`.
- `Core/ViewModel/Noification/PushNotificationPayload.cs` — dropped
  `RecipientType`/`GuestId` (everything is `UserId` now).

## 5. Firebase completion (Task 4)

- Added the `FirebaseAdmin` NuGet package (`Infrastructure.csproj`).
- `API/appsettings.json`/`appsettings.Development.json` are **gitignored** —
  the empty `Firebase`/`Notifications` sections added there only exist on
  this machine. Whatever manages real deployed config (secrets manager, CI
  variables, ops-owned appsettings) needs the same keys added independently.
- `FirebaseNotificationProvider` lazily initializes a `FirebaseApp` from
  config (`Firebase:ServiceAccountKeyPath` file path, or
  `Firebase:ServiceAccountJson` inline — see `appsettings.json`). If neither
  is set, it logs once and no-ops; SignalR delivery is unaffected either way,
  so an environment with no Firebase project configured still runs fine.
- Real `FirebaseMessaging.SendAsync` call per active, opt-in `Device` row for
  the recipient `UserId`.
- Dead-token handling: on `Unregistered`/`SenderIdMismatch` from FCM, the
  `Device` row is set `IsActive = false` (not deleted — history preserved) so
  it stops being retried.
- `Device` entity covers every field asked for in Task 4B: FCM token, device
  identifier, platform (now includes `web`, not just `ios`/`android`), device
  model, OS version, app version, last-active date, token-updated date,
  per-device notification-enabled flag, and multiple devices per user
  (already true of `GuestDevice`, now shared by staff too — the previous gap
  was that **only** guests had a device table at all).
- Delivery targeting (Task 4C) — already present, now unified onto `User`:
  single user (`SendToUserAsync`), multiple (`SendToUsersAsync`), role-based
  (`SendToRoleAsync`), permission-based (`SendToPermissionAsync`), broadcast
  (`BroadcastToAllUsersAsync`); guests reach all of these transparently since
  they're `User` rows.
- User lifecycle (Task 4D): `POST /api/v1/notifications/devices` registers a
  device for the current `User` (staff, driver, or guest token); `DELETE
  /api/v1/notifications/devices?token=...` deactivates one. Neither login nor
  logout auto-registers/deregisters a device today — that remains the
  client's responsibility (call register after obtaining a token, deregister
  before logging out) — no endpoint change to `AuthController`/`VipAppController`
  was needed or made for this.

## 6. Driver <-> Guest chat (Task 6) — `ChatController`

Its own controller (`API/Controllers/v1/ChatController.cs`, route `api/v1/chat`),
separate from `SupportChatController` — the two conversation types never appear
in each other's endpoints.

`POST /api/v1/chat/messages` (`[Authorize]` only — any driver or guest token).
Body: `SendDriverGuestMessageRequest` (`RecipientUserId?`, `RecipientRole?`,
`Body`, `AttachmentUrl?`, `AttachmentType?`). Response: the existing
`SupportMessageResponse`.

`GET /api/v1/chat/threads/{conversationId:guid}/messages?pageNumber=&pageSize=`
— the whole thread for either participant, paged (default 50), oldest-first
within the page. Each message carries `isMine` (`SenderUserId == callerUserId`)
so both sides render sent vs received off the same payload; `fromGuest` alone
can't, since both participants read the same rows. Non-participants get 403,
and an `AdminSupport` conversationId gets 404 — this endpoint only serves
`DriverGuest`.

- **Start/reuse**: `SupportChatService.GetOrCreateConversationAsync` looks up
  `(UserId=guestUserId, Type=DriverGuest, OtherUserId=driverUserId)`; creates
  it if missing, reuses (and reopens if `Closed`) otherwise.
- **Targeting**: `RecipientUserId` (either side's `User.PublicId`) works in
  both directions. `RecipientRole="driver"` is guest-only and resolves via
  the drivers on that guest's `Transport` rows (`Transport.DriverId` +
  `Transport.EventGuestId` are the only record of the pairing — there is no
  separate driver-pool table). Errors if zero or more than one driver is
  assigned, asking for an explicit `RecipientUserId` in the ambiguous case.
  A driver sender must always specify `RecipientUserId` — there's no single
  implicit guest for a driver the way there's a single implicit "any admin"
  for `AdminSupport`.
- **Authorization**: either way, the send is rejected (403) unless at least one
  non-deleted `Transport` links the driver's profile id to the target guest's
  user (`Transport.DriverId == driverProfile.Id` and
  `Transport.EventGuest.Guest.UserId == guestUserId`).
- **Validation**: unknown role -> 403; recipient not found -> 404; recipient
  inactive -> 400; recipient role doesn't match the expected counterpart
  (e.g. a guest targeting another guest) -> 400. **Blocked users are not
  implemented** — no such concept exists anywhere in this codebase today;
  flagging it here rather than adding a new feature unasked.
- **Performance**: the filtered unique index from §2 makes "does this
  conversation already exist" an index seek, and the same index prevents a
  duplicate conversation from ever being created under concurrent sends
  (a unique-constraint violation on the losing insert, not a race that
  produces two threads).

Still not included: a **list** of a caller's `DriverGuest` conversations (an
inbox). Reading a thread requires knowing its `conversationId`, which the client
gets from the send response or the new-message notification payload
(`data.conversationId`). Also not included: marking a `DriverGuest` thread read
— `UnreadByGuestCount`/`UnreadByAdminCount` are bumped on send but nothing
clears them for this type.

## 7. Endpoints affected

No endpoint was renamed and no request/response contract changed shape.

| Endpoint | Change |
|---|---|
| `POST /api/v1/support-chat/my/messages`, `GET my/conversations`, `GET my/messages`, `POST my/messages/read` | Internal only — now `Type=AdminSupport` filtered |
| `GET /api/v1/support-chat/conversations`, `GET .../messages`, `POST .../messages`, `POST conversations/by-guest/{guestId}/messages`, `POST .../read`, `.../close`, `.../reopen` | Internal only |
| `POST /api/v1/chat/messages` | **New** (Task 6) — was `POST /api/v1/support-chat/driver-guest/messages`, moved to `ChatController` |
| `GET /api/v1/chat/threads/{conversationId}/messages` | **New** — full driver↔guest thread with per-message `isMine` |
| `GET/PUT /api/v1/notifications/*` (staff) | Unaffected |
| `GET/PUT /api/v1/notifications/guest/*` | Internal only — reads/writes `Notifications` now |
| `POST /api/v1/notifications/guest/devices` | Internal only — writes `Devices` now |
| `POST /api/v1/notifications/devices`, `DELETE /api/v1/notifications/devices` | **New** — generic device register/deregister for any `User` |
| `POST /api/v1/notifications/send` | Unaffected (guest branch now resolves via `Devices`/`Notifications` transparently) |

## 8. Security considerations

- The guest JWT's `NameIdentifier` change closes a latent identity-collision
  risk noted in the pre-existing code (`CurrentGuest`/`ManualNotificationProvider`
  comments): before this change, a guest token's `NameIdentifier` (`Guest.Id`)
  could numerically collide with an unrelated staff `User.Id`, and anything
  that trusted `ICurrentUser`/`Clients.User(...)` off that claim without the
  extra role check `CurrentGuest` does would have resolved the wrong identity.
  It now always resolves to the guest's own dedicated `User` row.
- The `guest` role has zero permissions and `PortalAccess=false` — a guest
  token gaining a valid `ICurrentUser` resolution does not grant it access to
  any `[HasPermission]`-gated route.
- `api/v1/chat/*` is `[Authorize]`-only (no permission check, by design —
  drivers/guests hold no portal permissions) but the service itself rejects any
  sender whose role isn't `driver`/`guest`, every recipient lookup re-validates
  role and `IsActive`, and the thread read is gated on the caller being one of
  the conversation's two participants (`UserId` or `OtherUserId`).
- **Pre-existing, unrelated to this change, worth flagging**: `API/appsettings.json`
  and `appsettings.Development.json` contain a live-looking SQL Server
  connection string with a plaintext password, committed to the repo. Not
  touched here since it's out of scope, but should move to a secret store
  before this ships.

## 9. Performance considerations

- `Guests.UserId`, `Devices.Token` (unique), `Devices.(UserId, IsActive)`, and
  the two `SupportConversations` filtered unique indexes are all indexed for
  their actual lookup pattern — no full scans introduced.
- The `MERGE`-based backfill in the migration is a single set-based statement
  (not a per-row loop), so it scales with row count the same way the
  existing `AddSupportChatConversations` migration's own backfill did.
- Firebase push is per-device, best-effort, and one dead token no longer
  blocks a user's other devices (unchanged from before) — same fan-out shape
  as `GuestDevice`, just keyed by `UserId`.

## 10. Backward compatibility

- No DTO field was renamed or removed on any existing endpoint.
- `GuestId` in `SupportConversationSummaryResponse` still means "the guest's
  own public id" (now sourced via `User.GuestProfile.PublicId` instead of a
  direct FK, but the value and meaning are identical).
- `FromGuest` on `SupportMessageResponse` is unchanged in meaning.
- Every VIP-app guest route (`ICurrentGuest`-based) is untouched.
- `RoleDefinitions.RoleDef`'s new `PortalAccess` parameter defaults to `true`,
  so no existing role definition needed to change.

## 11. Testing strategy

- **Unit/service level**: `SupportChatService` — conversation get-or-create
  for both `AdminSupport` and `DriverGuest`; unread-counter increments on
  each send direction; mark-read scoping to the correct `Type`; validation
  branches in `SendDriverGuestMessageAsync` (missing target, role mismatch,
  inactive recipient, ambiguous assignment pool).
- **Migration**: apply against a snapshot of production-shaped data (guests
  with/without existing support messages, existing `GuestNotification`/
  `GuestDevice` rows) and assert: every `Guest.UserId` is set and points at a
  distinct, correctly-named `User`; `SupportConversations`/`SupportMessages`
  row counts are unchanged and every `UserId` matches the originating
  guest's new `UserId`; `Devices`/`Notifications` row counts grew by exactly
  the old `GuestDevices`/`GuestNotifications` row counts; both old tables are gone.
- **Integration**: guest OTP login -> support chat send/receive round-trip
  (SignalR `Clients.User` delivery, not the old group); admin reply flow
  end-to-end; driver<->guest send-then-reuse (second call to the same
  recipient must not create a second conversation).

## 12. Manual testing checklist

- [ ] Create a new guest (single + CSV import) -> confirm a linked `User`
      row exists with `RoleId` = `guest`, `Email`/`UserName` are `NULL`.
- [ ] Existing guest support-chat history (pre-migration) still loads in both
      the admin inbox and the guest's "my conversation" view, with the same
      messages/unread counts as before.
- [ ] Guest sends a support message -> admin inbox unread count increments;
      admin replies -> guest's push/SignalR notification arrives.
- [ ] Register a device via `POST /notifications/devices` for a staff user,
      a driver, and a guest token each -> confirm one `Devices` row each.
- [ ] Trigger a notification send (`SendToUserAsync`/`SendToRoleAsync`/
      `BroadcastToAllUsersAsync`) with Firebase configured against a real
      test project -> device receives the push; with Firebase unconfigured
      -> no crash, SignalR still delivers.
- [ ] Force an FCM `Unregistered` response (e.g. uninstall the app / use a
      stale token) -> confirm the `Devices` row flips `IsActive=false` and
      is skipped on the next send.
- [ ] Guest with one assigned driver sends `POST /chat/messages` with
      `recipientRole="driver"` -> conversation created; second message ->
      same conversation reused, no duplicate.
- [ ] Both sides `GET /chat/threads/{conversationId}/messages` -> same message
      list, `isMine` inverted between the two callers. A third user's token ->
      403. An `AdminSupport` conversationId -> 404.
- [ ] Guest with zero or multiple assigned drivers gets the expected error
      message for `recipientRole` targeting.
- [ ] Driver sends to a specific guest via `recipientUserId` -> conversation
      created/reused symmetrically; guest receives it in real time.
- [ ] Attempt `POST /chat/messages` with a staff (non-driver, non-guest)
      token -> 403.
- [ ] `RoleDefinitions`/`DataSeeder` run on a fresh database -> `guest` and
      `driver` roles both end up with `PortalAccess=false`.

## 13. Production deployment checklist

- [ ] **Back up the database** before applying the migration — it moves data
      across tables (`GuestDevices`/`GuestNotifications` -> `Devices`/
      `Notifications`) and mutates `SupportConversations`/`SupportMessages`.
- [ ] Deploy during a low-traffic window: the migration briefly drops/
      recreates `IX_Users_Email` and touches `SupportConversations`/
      `SupportMessages`/`Guests` — brief lock contention is expected on
      larger tables.
- [ ] Apply the migration (`dotnet ef database update` or the app's own
      migration-on-startup path, whichever this environment uses) **before**
      deploying the new application binaries — the code assumes `Guest.UserId`,
      `Devices`, and the `SupportConversations.Type` column already exist.
- [ ] After migration, spot-check: `SELECT COUNT(*) FROM Guests WHERE UserId
      IS NULL` = 0; `SELECT COUNT(*) FROM Guests g WHERE NOT EXISTS (SELECT 1
      FROM Users u WHERE u.Id = g.UserId)` = 0.
- [ ] Configure `Firebase:ServiceAccountKeyPath`/`ProjectId` in the target
      environment's configuration (not committed) if real device push is
      wanted at launch; otherwise leave blank — the app runs fine without it.
- [ ] Note the Down()-migration limitation in §2 before relying on rollback:
      safe only before any `DriverGuest` conversation has been created, and
      never restores split-back `GuestNotification`/`GuestDevice` data.
- [ ] Rotate the plaintext DB password currently committed in
      `appsettings.json`/`appsettings.Development.json` (pre-existing, unrelated
      to this change, flagged in §8) — recommended before or shortly after this
      deploy, not blocking it.
