# Shared context — session log

## 2026-07-30 — Guest/User merge, notification centralization, Firebase completion, driver<->guest chat

**What changed**: `Guest` is now a 1:1 profile extension of `User`
(`Guest.UserId`, mirrors `DriverProfile`). Guest-only chat/notification/device
tables (`GuestNotification`, `GuestDevice`) folded into the shared
`Notification`/new `Device` tables. `SupportConversation`/`SupportMessage`
re-keyed from `GuestId` to `UserId`, with a new `Type`
(`AdminSupport`/`DriverGuest`) so the same tables now also serve a new
driver<->guest chat endpoint. Firebase push is now a real `FirebaseAdmin` SDK
call (previously a stub). Full writeup: `docs/guest-user-merge-and-chat-refactor.md`.

**Decisions worth remembering**:
- Chose extension-table merge (Guest keeps its own table) over full identity
  merge, because `Guests.Email` has no uniqueness constraint (same person can
  have multiple Guest rows across events) — a full merge into `Users.Email`
  (which IS unique) would have collided.
- Guest JWT carries only `Guest.UserId` (`sub` + `NameIdentifier`) plus
  `role=="guest"`; no `Guest.Id` claim at all (`GuestClaims` deleted).
  `ICurrentGuest`/`RealTimeHubService` derive `Guest.Id` from `Guests.UserId`.
  Necessary for `Clients.User(...)`/`ICurrentUser` to resolve a guest, and it
  removes the Guest.Id/User.Id collision risk entirely. Keep `sub` and
  `NameIdentifier` identical — the JWT handler maps `sub` onto `NameIdentifier`,
  so a stale `sub` silently wins `FindFirst`.
- EF Core's SqlServer provider auto-filters a unique index to exclude NULLs
  whenever it covers a nullable column — caught this because it would have
  silently broken "one AdminSupport conversation per guest". Split into two
  filtered unique indexes instead of one composite index. Worth remembering
  for any future nullable-column unique index in this codebase.

**Open follow-ups not done (out of the requested scope)**:
- No list/inbox endpoint for `DriverGuest` conversations (task asked for one
  endpoint only — send/start-or-reuse).
- No "blocked users" concept exists anywhere in this codebase; not added.
- Login/logout don't auto-register/deregister a push device — client's
  responsibility via the new `POST/DELETE /api/v1/notifications/devices`.
- Pre-existing (unrelated) issue noticed: `appsettings.json`/`appsettings.Development.json`
  commit a live-looking DB connection string with a plaintext password. Not
  touched, flagged in the docs deliverable's Security section.

**State**: solution builds clean (`dotnet build gms.sln`, 0 errors). Migration
`20260730125545_MergeGuestIntoUserAndUnifyNotifications` is scaffolded and
hand-verified via `dotnet ef migrations script`, but **not yet applied** to
any database — do that as its own reviewed step, with a backup first (see
docs deliverable §13).

**Standing rule**: never push to development unless told.
