# DMS Backend API

Living record of the DMS mission-domain endpoints: what exists, what it validates,
and what it returns when it says no. Conventions first — they apply to every
endpoint below, so they are not repeated per route.

---

## 1. Conventions

### 1.1 Response envelope

Every endpoint returns the same shape, whether it succeeded or not:

```json
{
  "success": true,
  "message": "Operation successful",
  "data":    { },
  "errors":  [],
  "errorCode": null,
  "timestamp": "2026-09-23T13:04:37Z"
}
```

`data` is `null` on failure. `errorCode` is set only for conditions a client is
expected to branch on (see §9); ordinary validation failures carry a human
message and leave it null.

### 1.2 Status codes

| Code | Meaning | Produced by |
| --- | --- | --- |
| 200 | Succeeded | `SuccessResponse` |
| 400 | Rejected — failed validation or broke a rule | `ErrorResponse` |
| 401 | Not signed in, or the token expired | auth middleware |
| 403 | Signed in, but this role may not do it | `[HasPermission]` |
| 404 | The thing addressed does not exist | `NotFoundResponse` |
| 409 | Conflicts with current state | `ConflictResponse` |
| 500 | Unhandled — logged server-side, detail not returned | `ServerErrorResponse` |

> **Changed 2026-09-23.** `ErrorResponse` used to return **200** with
> `success:false`. The frontend's response interceptor unwraps any 2xx as a
> success and hands the caller `data`, so a validation failure arrived as a save
> that silently returned `null`. It now returns **400** and the existing error
> path (which already reads `message`, `errors` and `errorCode`) fires correctly.

> **Changed 2026-09-23.** `UnauthorizedMiddleware` used to rewrite every **403**
> into a **401**. Harmless when denials were rare; wrong under role-based
> read/write, where they are routine — the frontend retries once on 401 after
> refreshing the token, so a permission denial became a refresh and then a
> sign-out instead of a "forbidden" message. 401 and 403 are now distinct, and
> carry `errorCode` `UNAUTHORIZED` / `FORBIDDEN`.

### 1.3 Identifiers

Every id crossing the API is the entity's `PublicId` (a GUID). Internal integer
keys never leave the server. A malformed or unknown GUID is a 404, not a 500.

### 1.4 Authentication and permissions

All routes require a bearer token unless marked otherwise. Authorisation is
`[HasPermission(code)]` for a read and `[HasPermission(code, AccessLevel.Write)]`
for anything that changes state, where `code` is a `Permissions.Code` row.

Write never follows from read. Read does follow from write — a role that may edit
a page may obviously open it.

**Any-of.** A single attribute may list several codes, and holding *any* of them
admits the caller. This exists for reads that genuinely serve several menus (the
delegate roster is read by Accreditation, Seating, Meetings, Services…). Two
stacked attributes mean AND, so a single attribute is the only way to say OR.

### 1.5 Locations

Anything location-shaped is a foreign key into `Locations`, never free text. If
the place does not exist yet, create it first (`POST /v1/lookups/locations`) and
pass the id back.

### 1.6 Soft delete

`DELETE` marks the row deleted; it does not remove it. Unique indexes are
filtered on `IsDeleted = 0`, so a name freed by a delete can be reused.

---

## 2. Departments

Reference data. A person belongs to one department; a Department Head nominates
only from their own.

`GET` is open to any signed-in user — the dropdown appears on the delegate form
and on Nominations. Writes need **write on `lookups`**.

| Method | Route | Permission |
| --- | --- | --- |
| GET | `/api/v1/departments` | any signed-in user |
| GET | `/api/v1/departments/{id}` | any signed-in user |
| POST | `/api/v1/departments` | `lookups` write |
| PUT | `/api/v1/departments/{id}` | `lookups` write |
| DELETE | `/api/v1/departments/{id}` | `lookups` write |

**Response** — `id`, `name`, `nameAr`, `memberCount`.

`memberCount` is how many people are currently in it, which is what makes the
delete rule below visible before you hit it.

### Validation

| Rule | Result |
| --- | --- |
| `name` required | 400 *Name is required.* |
| `name` ≤ 150 chars | 400 |
| `name` unique among non-deleted | 409 `DEPARTMENT_NAME_CONFLICT` |
| Delete while people are still in it | 409 `DEPARTMENT_IN_USE` |

The delete rule matters: `Guest.DepartmentId` is `SET NULL` on delete, so without
it, deleting a department would silently blank the department on everyone in it
rather than failing.

---

## 3. External Invitations (Phase 1)

Invitations received **from** a host organisation, and converting one into a
mission.

> Not to be confused with `InvitationController`, which is the outbound RSVP sent
> to a delegate. These are inbound and unrelated. They coexist.

| Method | Route | Permission |
| --- | --- | --- |
| GET | `/api/v1/external-invitations` | `external-invitations` |
| GET | `/api/v1/external-invitations/{id}` | `external-invitations` |
| POST | `/api/v1/external-invitations` | `external-invitations` write |
| PUT | `/api/v1/external-invitations/{id}` | `external-invitations` write |
| DELETE | `/api/v1/external-invitations/{id}` | `external-invitations` write |
| POST | `/api/v1/external-invitations/{id}/decline` | `external-invitations` write |
| POST | `/api/v1/external-invitations/{id}/convert` | `external-invitations` write **AND** `events` write |

The list is paged (`pageNumber`, `pageSize`, `searchTerm`) and takes an optional
`status` filter. `searchTerm` matches mission title or host organisation.

Convert needs write on **both** menus — it is the one endpoint that creates a
mission from outside the Missions module, so holding only the invitations menu is
not enough.

### Lifecycle

```
logged ──convert──► converted   (a mission now exists; terminal)
   │
   └───decline────► declined    (no mission; terminal)
```

Once `converted`, the invitation is the mission's provenance and is **frozen** —
edit, delete and re-convert are all refused. That is deliberate: an editable
invitation would silently disagree with the mission it produced.

### Validation

| Rule | Result |
| --- | --- |
| `hostOrganization` required, ≤ 300 | 400 |
| `missionTitle` required, ≤ 300 | 400 |
| `hostEmail` must contain `@` if given | 400 |
| `headcountCap` ≥ 1 if given | 400 |
| `endDate` not before `startDate` | 400 |
| `destinationId` must be a real `Locations` row | 404 |
| Edit / delete / re-convert once converted | 409 `INVITATION_ALREADY_CONVERTED` |
| Convert a declined invitation | 409 `INVITATION_DECLINED` |

`headcountCap` and `responseDeadline` are **nullable** — a host letter often
states neither. (They were briefly non-nullable; a non-nullable `DateOnly`
defaults to `0001-01-01`, which reads as a real deadline already past.)

### Convert

Everything defaults from the invitation; the request body only overrides:

| Body field | Falls back to |
| --- | --- |
| `title` | invitation's `missionTitle` |
| `startDate` / `endDate` | invitation's dates |
| `destinationId` | invitation's destination |
| `delegationCap` | invitation's `headcountCap` |
| `type`, `venueId`, `venueName`, `guestModel` | none — optional |

`hostName` and `hostEmail` always come from the invitation.

The mission is created through `IEventService.CreateEventAsync`, so slug
generation, venue resolution and status validation stay in one place rather than
being duplicated here. The whole thing runs in a **transaction**: if linking the
two fails, no orphan mission is left behind.

Both sides are linked — `Event.HostInvitationId` and
`HostInvitation.ConvertedEventId` — so either can be navigated without a lookup.

`decline` appends its reason to `notes` rather than overwriting, so the record of
why survives.

---

## 4. Missions (Event)

An `Event` **is** a mission. The existing endpoints carry the mission fields.

### 4.1 New fields

| Field | Notes |
| --- | --- |
| `destinationId` / `destinationAddress` | `Locations` row — where the delegation travels. Distinct from `venueName`, which is the building |
| `delegationCap` | Headcount cap from the host. Enforced when nominating |
| `destinationTier` | `regional` / `international_a\|b\|c` — bands per-diem and approval routing |
| `costCenter` | The budget line charged. Free text: it references a finance system DMS does not own, so it is recorded, not validated |
| `fundingModel` | `org_paid` / `hosted` / `mixed` — who pays decides which services get booked at all |
| `hostName`, `hostEmail` | Where the nomination letter goes |
| `hostInvitationId` | The invitation it came from, if any |
| `isCompleted` | **Derived, never stored** — true once `endDate` has passed |

`isCompleted` is computed at read time from `endDate`. A completed mission takes
no new delegates or bookings, but still accepts post-mission reports — reporting
is by definition what happens afterwards.

### 4.2 Validation added

| Rule | Result |
| --- | --- |
| `delegationCap` ≥ 1 | 400 |
| `destinationId` must be a real `Locations` row | 404 |
| `destinationTier` / `fundingModel` outside their option set | 400, listing the valid values |
| Lowering `delegationCap` below the roster already nominated | 409 `DELEGATION_CAP_BELOW_ROSTER` |

The last one is the interesting case: without it a mission could be left
permanently over capacity with no way back.

### 4.3 `GET /api/v1/Events` is deliberately open

Not gated on `events`. The mission switcher sits in the app chrome on every
screen and every mission-scoped module needs the list to resolve the active
mission, so gating it would break all of them for anyone without the Missions
menu. Reading the list is not sensitive; every write is gated.

---

## 5. Nominations (Phase 2)

Assembling a mission's delegation. A nomination **is** an `EventGuest` row — the
same participation every other mission-scoped record hangs off. Nominating fills
the mission-role fields on it; services, accreditation and seating fill the rest
later.

| Method | Route | Permission |
| --- | --- | --- |
| GET | `/api/v1/nominations?eventId=` | any-of read: `nominations`, `guests` |
| GET | `/api/v1/nominations/candidates?eventId=&departmentId=` | `nominations` |
| GET | `/api/v1/nominations/roles` | any signed-in user |
| POST | `/api/v1/nominations` | `nominations` write |
| PUT | `/api/v1/nominations/{id}` | `nominations` write |
| DELETE | `/api/v1/nominations/{id}` | `nominations` write |

`{id}` is the **participation** id, not the person id. `personId` on the way in
is the person (`Guest.PublicId`); `id` on the way out is the participation.

**Candidates** returns people *not already on this mission* — the roster is the
exclusion list, so re-nominating from the picker is impossible rather than merely
rejected afterwards. `departmentId` narrows it, which is how a Department Head is
scoped to their own staff.

**Mission roles** are `Roles` rows flagged `IsDelegateRole`. There is no separate
delegate-type lookup: the same row that names the role also carries whatever
portal access it should come with. See §5.2.

### 5.1 Derived flags

Every roster row carries a `flags` object. All of it is computed at read time and
**never stored**, so a renewed passport or a cancelled mission changes the answer
with no write anywhere.

| Flag | Meaning |
| --- | --- |
| `passportMissing` | No passport number or no expiry on file |
| `passportExpired` | Expires before the mission starts |
| `passportExpiringSoon` | Expires within **6 months** of the mission start (the workflow's rule) |
| `overlappingMission` + `overlappingMissions` | On another mission whose dates intersect this one |

The reference date is the mission's `startDate`, falling back to today when it has
none. Missions without both dates cannot overlap and are skipped.

### 5.2 Validation

| Rule | Result |
| --- | --- |
| `eventId` / `personId` required | 400 |
| Mission or person unknown | 404 |
| Mission has ended | 409 `MISSION_COMPLETED` |
| Person already on this mission | 409 `ALREADY_ON_MISSION` |
| Roster already at the host's cap | 409 `DELEGATION_CAP_REACHED` |
| Mission role is not flagged `IsDelegateRole` | 400 |
| A second Head of Delegation | 409 `HEAD_OF_DELEGATION_TAKEN` |
| Remove someone with bookings | 409 `NOMINATION_HAS_BOOKINGS` |

**Mission completed** blocks new delegates only. Reports stay open — that is by
definition what happens afterwards — so the guard lives here, not on the mission.

**One Head of Delegation per mission** is enforced because protocol order depends
on there being exactly one. Relax it in `NominationService.ResolveMissionRoleAsync`
if a mission ever needs co-heads.

**Remove** is refused while the delegate has flights, accommodation, transport or
a seat assignment. Removing anyway would either orphan those or cascade them away
silently; making the coordinator cancel them first is the lesser evil. The message
names which ones are blocking.

Nomination also survives a race: the unique index on `(GuestId, EventId)` turns a
concurrent double-nomination into `ALREADY_ON_MISSION` rather than two rows.

---

## 6. HR Verification (Phase 2)

HR's sign-off on the roster. A nomination lands as `pending`; HR either confirms
the passport, grade, visa and insurance records or sends it back with a note.

| Method | Route | Permission |
| --- | --- | --- |
| GET | `/api/v1/hr-verification?eventId=&status=` | `hr-verification` |
| POST | `/api/v1/hr-verification/verify` | `hr-verification` write |
| POST | `/api/v1/hr-verification/reject` | `hr-verification` write |

`status` filters to `pending` / `verified` / `rejected`. The rows are the same
shape as the roster, flags included — HR needs the passport warnings to decide.

**Both are bulk.** HR works through a list, so a single id is a batch of one:

```json
{ "ids": ["<participation-id>", "..."], "note": "Records confirmed" }
```

**Partial success is normal.** Ids that are unknown, or already in the target
state, are returned as skips rather than failing the batch — two people working
the same list must not undo each other:

```json
{ "updated": 3, "skipped": 1,
  "skips": [ { "id": "…", "reason": "Already verified." } ] }
```

| Rule | Result |
| --- | --- |
| `ids` empty | 400 |
| Reject with no `note` | 400 — a rejection with no reason is not actionable |
| Id unknown | counted as a skip, not an error |
| Already in that state | counted as a skip |

Verifying stamps `hrVerifiedOn` and `hrVerifiedBy` alongside the status.

---

## 7. Nomination Letter (Phase 4)

The official letter naming the delegation, and its back-and-forth with the host.
One letter per mission.

| Method | Route | Permission |
| --- | --- | --- |
| GET | `/api/v1/nomination-letter?eventId=` | `nomination-letter` |
| GET | `/api/v1/nomination-letter/versions/{versionId}` | `nomination-letter` |
| POST | `/api/v1/nomination-letter/generate` | `nomination-letter` write |
| POST | `/api/v1/nomination-letter/send` | `nomination-letter` write |
| POST | `/api/v1/nomination-letter/acknowledge` | `nomination-letter` write |
| POST | `/api/v1/nomination-letter/request-changes` | `nomination-letter` write |

`GET` on a mission with no letter returns a `not_generated` **shell** rather than
a 404, so the screen renders before anyone presses Generate.

### 7.1 Lifecycle

```
not_generated ──generate──► draft ──send──► sent ──acknowledge──────► acknowledged
                              ▲               │                            │
                              │               └──request-changes──► changes_requested
                              │                                            │
                              └──── generate (new version) ◄───────────────┘
                                          ▲         also re-send ──► sent
                                          │
                              acknowledged ┘  (roster moved on → re-issue)
```

The host replies **by email, outside this system**, so acknowledge and
request-changes are recorded by Protocol rather than detected.

**Re-sending is allowed** from `sent` and `changes_requested` — that is exactly
the "resend after they asked for changes" step, and it logs as *Re-sent*.

**Generating a new version restarts the track at `draft`**, even from
`acknowledged`, and clears `sentOn` / `respondedOn` / `messageId`. What the host
agreed to is no longer what the letter says, so the new version is unsent by
definition. Status always describes the **current** version.

### 7.2 Versions are immutable snapshots

Each generate pins the roster **as it stood**, as JSON on the version row. It is
snapshotted rather than joined because the live roster moves on and v1 must keep
showing the names the host was actually sent. Verified: after removing a delegate,
the v2 snapshot still lists them while the live roster does not.

`GET /versions/{id}` returns that pinned roster — name, job title, mission role,
subgroup, department, nationality, passport number and expiry.

Three fields on the main response make staleness visible without a diff:

| Field | Meaning |
| --- | --- |
| `versionRosterCount` | Size of the roster pinned in the current version |
| `liveRosterCount` | Size of the roster right now |
| `rosterChangedSinceGenerated` | The two disagree — what the host holds is out of date |

### 7.3 Validation

| Rule | Result |
| --- | --- |
| Roster empty at generate | 400 |
| Any nomination HR-**rejected** | 409 `ROSTER_HAS_REJECTED` |
| `language` not `en` / `ar` | 400 |
| Send / acknowledge / request-changes before generating | 409 `LETTER_NOT_GENERATED` |
| Send with no host email anywhere | 400 |
| Host email malformed | 400 |
| Send once acknowledged | 409 `LETTER_ALREADY_ACKNOWLEDGED` |
| Acknowledge or request-changes before sending | 409 `LETTER_NOT_SENT` |
| Request changes with no note | 400 |

**Rejected blocks, pending does not.** A nomination HR turned down must never
reach the host; pending is fine, because HR often signs off while the letter is
being drafted.

Send falls back to the mission's `hostEmail`; `hostEmail` in the body overrides it
for that send only and is not written back to the mission.

### 7.4 History

Every transition appends to an append-only log — generated, sent, re-sent, host
acknowledged, host requested changes — each with the version, the actor, the
timestamp and the detail (recipient, message id, or the host's note). Returned
newest-first on the main response.

Document rendering is **out of scope here**: generate pins the roster and bumps
the version; `documentUrl` is set by whatever produces the PDF, and may be passed
on the generate call.

---

## 8. Readiness (Phase 6)

The pre-departure gate. Five checks per delegate, all **derived on read** from the
real booking data — nothing about readiness is stored, so it cannot drift from
what the services layer actually holds.

| Method | Route | Gate |
| --- | --- | --- |
| GET | `/api/v1/readiness?eventId={guid}&onlyNotReady=false` | `readiness` read |
| GET | `/api/v1/readiness/summary?eventId={guid}` | `readiness` read |
| POST | `/api/v1/readiness/waive` | `readiness` write |
| DELETE | `/api/v1/readiness/waivers/{waiverId:guid}` | `readiness` write |

### 8.1 The five checks

| Item | Met when |
| --- | --- |
| `passport` | Passport number and expiry are on file, and the expiry is **on or after the mission start date** |
| `visa` | `visaRequired` is false, **or** `visaStatus` is `active` / `expiring-soon` |
| `flight` | A flight row exists for the participation |
| `accommodation` | An accommodation row exists for the participation |
| `transport` | A transport row exists for the participation |

Passport is judged against the mission start, not today: it has to be valid when
they travel, which is the only date that matters. A mission with no start date
falls back to today.

`expiring-soon` counts as met for visa — the document is valid, the warning is a
prompt to renew, not a bar to travel. `visaRequired = false` is how "no visa
needed" is expressed, since the status set has no such value.

Each item comes back as `met` | `waived` | `missing`, with a `detail` string for
display ("Valid to 2029-04-30", "No room assigned"). A waived item also carries
`waiverId`, `waiverReason`, `waivedByName` and `waivedAt`.

Two derived booleans per delegate:

- `travelReady` — no item is `missing` (waived counts as cleared)
- `readyWithWaivers` — travel-ready, but only because something was waived

Ready is ready; the second flag exists so a reviewer can see *how* it was reached.

The whole mission is built in a fixed number of queries — roster, one per booking
type, one for waivers — not per delegate.

### 8.2 Summary

`GET /readiness/summary` returns `total`, `travelReady`, `notReady`,
`readyWithWaivers`, plus `missingByItem` — a count per item key, so the screen can
show "4 without flights" without the client tallying rows itself. Waived items are
**not** counted as missing.

### 8.3 Waivers

A waiver is the only stored readiness state: a coordinator overriding one red item,
with a reason that is required in the service *and* in the database. That log is
the only record of why a delegate travelled without something the checklist
demanded.

One waiver per `(delegate, item)`, enforced by a filtered unique index on
`[IsDeleted] = 0`. The mission is implied by the participation, so that pair is the
whole rule. Withdrawal is a soft delete, so the same item can be waived again
afterwards with a fresh reason rather than resurrecting the old one.

### 8.4 Validation

| Rule | Response |
| --- | --- |
| Unknown `eventId` / `id` / `waiverId` | 404 |
| `id` or `waiverId` empty | 400 |
| `itemKey` not one of the five | 400, listing the valid keys |
| `reason` missing or blank | 400 |
| Item already waived for that delegate | 409 `ITEM_ALREADY_WAIVED` |
| Item is already met | 409 `ITEM_ALREADY_MET` |

Waiving an already-met item is refused rather than ignored: it would leave a
misleading "travelled without it" note against someone who had it all along.

Both write endpoints return the delegate's **recomputed** row, so the client never
has to re-fetch to see the effect.

---

## 9. On-Mission Ops (Phase 7)

Three separate screens, three separate menus, three separate gates — grouped here
because they all happen while the delegation is on the ground.

| Feature | Gate | Menu label |
| --- | --- | --- |
| Gathering notices & headcount | `on-mission-ops` | On-Mission Ops |
| Incidents | `incidents` | Help Requests |
| Field decisions | `head-of-delegation` | Head of Delegation |

### 9.1 Gathering notifications

| Method | Route |
| --- | --- |
| GET | `/api/v1/on-mission-ops/subgroups?eventId={guid}` |
| GET | `/api/v1/on-mission-ops/notifications?eventId={guid}` |
| POST | `/api/v1/on-mission-ops/notifications` |

This table is the coordinator's **outbox**, not a delivery mechanism. Sending fans
the message out through the existing notification machinery — one persisted row
per delegate, pushed to their devices — and then records one row here saying what
went to the group. The existing `Notifications` table answers "what did this person
receive"; the ops screen needs "what did I send".

The fan-out targets `Guest.Id`, resolved from the roster's `EventGuest.GuestId`.
Both are bare ints from the same sequence, so passing a participation id would not
fail — it would quietly notify an unrelated person.

`recipientCount` is what **actually went out**, taken from the fan-out's result
rather than from the roster count, and it is frozen at send time: the roster may
change afterwards, the record of who was reached should not.

If the fan-out throws, nothing is recorded and the call returns 500 — an outbox
entry for a message that never went is worse than no entry.

`subgroup` omitted targets the whole delegation. `GET /subgroups` returns each
subgroup with its headcount (unassigned delegates last, under a null key), which
drives both the target picker and the headcount panel.

| Rule | Response |
| --- | --- |
| Unknown `eventId` | 404 |
| `message` missing or blank | 400 |
| `message` over 1000 characters | 400 |
| Nobody matches the target | 400 `NO_RECIPIENTS` |

### 9.2 Incidents

| Method | Route |
| --- | --- |
| GET | `/api/v1/incidents?eventId=&status=&severity=&category=&delegateId=` |
| GET | `/api/v1/incidents/summary?eventId={guid}` |
| GET | `/api/v1/incidents/{id:guid}` |
| POST | `/api/v1/incidents` |
| PUT | `/api/v1/incidents/{id:guid}` |
| POST | `/api/v1/incidents/{id:guid}/status` |
| DELETE | `/api/v1/incidents/{id:guid}` |

Option sets: category `late` / `lost` / `medical` / `other`, severity `low` /
`medium` / `high`, status `open` / `in_progress` / `resolved` / `escalated`.

`delegateId` is **optional on create** — an incident may concern the delegation
rather than one person, which is why the participation link is nullable. When
given, it must be a participation **on that mission**; one from another mission is
a caller error, not a cross-mission link.

`raisedVia` records whether the delegate reported it themselves (`app`) or an
officer logged it for them (`portal`) — materially different when the mission is
reviewed. This controller always writes `portal`; `app` is reserved for the
delegate-facing path.

**Status moves.** Any status may follow any other; only a no-op is refused. Two
rules carry weight:

- Resolving **requires a note** — the log has to say how it ended.
- `resolved` is the only status that closes an incident. **`escalated` is still
  open**, just somebody else's problem now; `isOpen` and the summary's `open`
  count both follow that rule.

A note is appended to the description as a dated, status-stamped line rather than
stored separately, so the incident reads as one narrative. The column is capped at
2000 characters; if appending would overflow, the **oldest** text is dropped, never
the note explaining the move.

Resolving stamps `resolvedBy` / `resolvedAt`; moving back off resolved **clears**
them, because the old resolution no longer describes the incident.

Lists come back open-first then newest. `PUT` is a partial update: omitted fields
are left alone, and `delegateId: "00000000-0000-0000-0000-000000000000"` detaches
the incident from its delegate (omitting it changes nothing).

`GET /summary` returns `total`, `open`, `resolved` plus `byStatus`, `bySeverity`
and `byCategory`, each carrying **every** key including the zeroes, so the client
renders a stable set of tiles.

`DELETE` is a soft delete for one logged by mistake. A real incident is resolved,
not deleted.

### 9.3 Field decisions

| Method | Route |
| --- | --- |
| GET | `/api/v1/field-decisions?eventId={guid}` |
| POST | `/api/v1/field-decisions` |
| PUT | `/api/v1/field-decisions/{id:guid}` |
| DELETE | `/api/v1/field-decisions/{id:guid}` |

Deliberately thin: no approval, no status, no reversal. The decision was taken in
the field before anyone typed it in, so this records it rather than governing it.
A decision that is later changed is a **second entry**, not an edit of the first —
`PUT` exists to correct what was typed, not to rewrite history.

`decidedAt` is optional and defaults to now. It is separate from `createdAt`
because these are logged after the fact, and the log is ordered by when the
decision was **taken**. A date more than five minutes in the future is refused;
the tolerance is for a client clock running ahead.

| Rule | Response |
| --- | --- |
| Unknown `eventId` / `id` | 404 |
| `decisionNote` missing or blank | 400 |
| `decisionNote` over 2000 characters | 400 |
| `decidedAt` in the future | 400 |

---

## 10. Cross-module reads

Some endpoints are read by modules other than the one that owns them. They were
previously commented out entirely (so: open to anyone signed in); they are now
gated with **any-of**, which keeps the dependency working without opening them up.

| Endpoint | Gate |
| --- | --- |
| `GET /v1/Guest`, `GET /v1/Guest/{id}` | any-of read: `guests`, `nominations`, `hr-verification`, `accreditation`, `seating`, `meetings`, `services`, `transportation`, `readiness` |
| `GET /v1/Guest/other-events` | any-of read: `guests`, `nominations` |
| `GET /v1/venue`, `GET /v1/venue/{id}` | any-of read: `venue-config`, `venues`, `events`, `seating`, `meetings` |
| `POST /v1/lookups/locations` | any-of **write**: `lookups`, `events`, `services`, `transportation`, `venue-config`, `venues` |
| `GET /v1/Events` | open to any signed-in user (§4.3) |

The location write is any-of by necessity: locations are created on the fly by
whichever module needs a place that does not exist yet — a mission's destination,
a venue, a hotel, a pickup point. It is still a gate; a role with write on none of
those cannot create locations.

---

## 10.1 Dynamic service forms: the `file` field type

`ServiceFieldTypes` gained **`file`** — an uploaded document, for the Visa
service's issued visa and anything like it (a ticket, a signed letter).

The stored value is the **blob URL**, exactly like every other attachment in the
system (`Event.AttachmentUrl`, `HostInvitation.AttachmentUrl`): the client
uploads first and saves the URL it gets back, and `BlobSasMiddleware` re-signs it
on read. Nothing about the file itself lives in the values column.

- `ServiceFieldDefinition.Accept` carries an HTML accept string (`".pdf,.jpg"`)
  so the picker can narrow the dialog. It is a hint, not a guarantee — the blob
  layer decides what it will actually store, and already handles PDFs.
- `ConstraintErrors` rejects a value that is not an http(s) URL. That is what
  stops a stale client persisting `"visa.pdf"`, which would render as a broken
  link forever after.
- The client strips the SAS token before saving (`stripFileTokens`): the token
  makes the preview link work while the form is open, and persisting one would
  bake in an expiry.

Uploads go through the existing `POST /v1/upload/image`. The route and its
response field are named "image" for historical reasons only.

---

## 11. Error codes

Machine-readable, for clients that need to branch rather than just display.

| Code | Meaning |
| --- | --- |
| `UNAUTHORIZED` | Not signed in / token expired |
| `FORBIDDEN` | Signed in, role lacks the permission |
| `DEPARTMENT_NAME_CONFLICT` | A department with that name exists |
| `DEPARTMENT_IN_USE` | People are still assigned to it |
| `INVITATION_ALREADY_CONVERTED` | Already became a mission; frozen |
| `INVITATION_DECLINED` | Was declined; cannot convert |
| `DELEGATION_CAP_BELOW_ROSTER` | Cap would be below those already nominated |
| `MISSION_COMPLETED` | Mission has ended; takes no new delegates |
| `ALREADY_ON_MISSION` | That person is already on the roster |
| `DELEGATION_CAP_REACHED` | The host's cap is already used up |
| `HEAD_OF_DELEGATION_TAKEN` | The mission already has one |
| `NOMINATION_HAS_BOOKINGS` | Delegate still has flights / hotel / transport / seat |
| `ROSTER_HAS_REJECTED` | Roster contains HR-rejected nominations; cannot go to the host |
| `LETTER_NOT_GENERATED` | No version issued yet |
| `LETTER_NOT_SENT` | Cannot record a host reply before sending |
| `LETTER_ALREADY_ACKNOWLEDGED` | Generate a new version to send again |
| `ITEM_ALREADY_WAIVED` | That readiness item is already waived for the delegate |
| `ITEM_ALREADY_MET` | Nothing to waive — the item is satisfied |
| `NO_RECIPIENTS` | Nobody matches the broadcast target |
| `INCIDENT_STATUS_UNCHANGED` | The incident is already in that status |

Pre-existing, unchanged: `GUEST_ALREADY_ON_EVENT`, `GUEST_EMAIL_CONFLICT`,
`SERVICE_LEVEL_RULE`.

---

## 12. Error handling

Validation lives in the **service**, not the controller — controllers only bind,
call and return. Services return an `ApiResponse` rather than throwing, so the
status code is a property of the decision rather than of an exception filter.

Exceptions are for the genuinely unexpected. Where one is caught (the convert
transaction), the transaction is rolled back, the exception is logged with its
context, and the caller gets a 500 with a generic message — never the exception
text.

---

## 13. Built / not built

**Built and tested**

- Departments — full CRUD, in-use guard
- External Invitations — CRUD, decline, convert-to-mission
- Mission fields on Event — create, update, read, cap guard
- Nominations — roster, candidate picker, mission roles, derived flags, cap and HoD guards
- HR Verification — filtered list, bulk verify/reject with partial success
- Nomination Letter — generate/version/send/acknowledge/request-changes, immutable roster snapshots, audit log
- Readiness — five derived checks, summary counts, waive / withdraw
- On-mission ops — gathering notices with real fan-out, subgroup headcount, incidents with status track and summary, field decision log
- Dynamic service forms — `file` field type for uploaded documents (§10.1)
- Permissions model, role access, seed (see `role-access` docs and the seed files)

**Not built yet** — entities and migration exist, endpoints do not:

| Area | Tables waiting |
| --- | --- |
| Reports | `PostMissionReports`, `CombinedReports` |

**Known gaps**

- `notifications` and `logs` have no seeded permission row, so
  `POST /v1/notifications/send` cannot currently be granted to anyone. Delegation
  broadcasts are unaffected — they go through `on-mission-ops` (§9.1), which is
  seeded.
- Incidents can only be raised through the portal. `IncidentChannels.App` exists
  and the column accepts it, but no delegate-facing endpoint writes it yet, so
  every row reads `portal`.
- `financials` is deliberately unseeded — the finance track is out of DMS scope,
  so those inherited endpoints stay unreachable rather than being re-pointed.
- The `lookup-*` menu rows are navigation only. No endpoint gates on them
  individually, so granting `lookup-element-types` without `venue-config` shows
  the menu and then 403s on open.
