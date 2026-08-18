# Firestore: `rideRequests`

Live projection of a ground transfer (SQL `Transports` row) so the driver app and
the VIP guest app can snapshot-listen instead of polling.

- **Collection:** `rideRequests`
- **Document ID:** the transport's `PublicId` (the same GUID the REST API returns
  as `id`, and the same one every `/api/v1/transport-app/jobs/{id}/...` route takes)
- **Written by:** the backend, via `RideMirror` (Admin SDK — bypasses security rules)
- **Except `currentLocation`:** written by the driver app directly from the client SDK

SQL Server stays the source of truth. This projection is one-way and best-effort:
if a mirror write fails it is logged and swallowed, and the next status change on
that ride rewrites the whole document.

---

## Document shape

```jsonc
{
  "id":         "8f3c...-...-...",   // string  — transport PublicId (== document id)
  "jobNumber":  "VIP-142",           // string  — display label, "VIP-" + internal id
  "status":     "new",               // string  — see lifecycle below
  "rideSource": "on-demand",         // string  — "on-demand" (guest) | "scheduled" (admin)
  "notes":      null,                // string? — free text from dispatch

  "eventId":    "1a2b...",           // string? — event PublicId
  "eventName":  "Formula 1 Weekend", // string?

  // The ride's participation (EventGuest PublicId). Also mirrored inside
  // guest{} so a client holding only that object still has it.
  "eventGuestId": "5e7f...",         // string  — EventGuest PublicId

  "guest": {
    "id":       "9c4d...",           // string  — PERSON id (Guest PublicId)
    "eventGuestId": "5e7f...",       // string  — this ride's participation
    "name":     "Ayesha Khan",       // string
    "tier":     "VVIP",              // string?  — per event, from EventGuest
    "phone":    "+9712...",          // string?
    "photoUrl": "https://..."        // string?
  },

  "pickup": {
    "address":  "Hilton, Corniche",  // string?
    "lat":      "24.4539",           // string?  ⚠ string, not number — see note
    "lng":      "54.3773"            // string?
  },
  "dropoff": {
    "address":  "Yas Marina Circuit",
    "lat":      "24.4672",
    "lng":      "54.6031"
  },

  "pickupTime":        "<timestamp>", // Timestamp? — planned
  "dropoffTime":       "<timestamp>", // Timestamp?
  "actualPickupTime":  "<timestamp>", // Timestamp? — stamped at start-trip
  "actualDropOffTime": "<timestamp>", // Timestamp? — stamped at complete

  "vehicle": {
    "number": "AD-12345",            // string?
    "model":  "Mercedes S-Class"     // string?
  },

  "driverId": "7e1f...",             // string?  — driver PublicId, null until accepted
  "driver": {                        // map?     — the whole map is null until accepted
    "id":       "7e1f...",
    "name":     "Bilal Ahmed",
    "phone":    "+9715...",
    "photoUrl": "https://..."
  },

  "currentLocation": {               // map?     — absent until the driver first writes
    "latitude":  24.4612,            // number
    "longitude": 54.4021,            // number
    "updatedAt": "<timestamp>"
  },

  "updatedAt": "<timestamp>"         // Timestamp — last backend mirror write
}
```

### Notes on types

- `pickup.lat` / `pickup.lng` / `dropoff.lat` / `dropoff.lng` are **strings**,
  because the SQL `Locations` table stores them as `nvarchar`. `currentLocation`
  is **numbers**, because the driver app writes it. Parse accordingly — this is
  not a typo.
- `driverId` is duplicated outside `driver{}` on purpose: Firestore equality
  filters need a top-level field, and the driver app's "my jobs" listener filters
  on it.
- `currentLocation` is the only key the backend never writes. It simply does not
  exist on a document until the assigned driver pushes a position, so guest apps
  must handle its absence, not just a null.

---

## Worked example

One ride, same document, three moments. Timestamp fields are real Firestore
`Timestamp` values — shown here as ISO-8601 for readability, not as strings.

### 1. Guest submits the request — `status: "new"`

`POST /api/v1/vip-app/transport-requests` has just returned.

```json
{
  "id": "8f3c1d7a-4b2e-4f19-9c50-6d1f2a7b3e44",
  "jobNumber": "VIP-142",
  "status": "new",
  "rideSource": "on-demand",
  "notes": null,

  "eventId": "1a2b8c9d-33e4-4a7b-9f10-5c6d7e8f9a0b",
  "eventName": "Formula 1 Weekend",
  "eventGuestId": "5e7f6a1b-2c3d-4e5f-8a9b-0c1d2e3f4a5b",

  "guest": {
    "id": "9c4d5e6f-7a8b-4c9d-8e1f-2a3b4c5d6e7f",
    "eventGuestId": "5e7f6a1b-2c3d-4e5f-8a9b-0c1d2e3f4a5b",
    "name": "Ayesha Khan",
    "tier": "VVIP",
    "phone": "+971501234567",
    "photoUrl": "https://gmsstorage.blob.core.windows.net/guests/9c4d5e6f.jpg"
  },

  "pickup": {
    "address": "Hilton Abu Dhabi Yas Island, Yas Island",
    "lat": "24.4996",
    "lng": "54.6070"
  },
  "dropoff": {
    "address": "Yas Marina Circuit, Gate 3",
    "lat": "24.4672",
    "lng": "54.6031"
  },

  "pickupTime": "2026-08-12T09:30:00Z",
  "dropoffTime": null,
  "actualPickupTime": null,
  "actualDropOffTime": null,

  "vehicle": { "number": null, "model": null },

  "driverId": null,
  "driver": null,

  "updatedAt": "2026-08-12T08:41:17Z"
}
```

No `currentLocation` key at all — nothing has written one yet.
`vehicle` is all-null because the guest didn't pick a car; dispatch can fill it later.

### 2. Driver accepts — `status: "assigned"`

`POST /api/v1/transport-app/jobs/8f3c1d7a-.../accept`. Only the changed keys shown:

```json
{
  "status": "assigned",
  "driverId": "7e1f9a2b-5c6d-4e8f-9012-3a4b5c6d7e8f",
  "driver": {
    "id": "7e1f9a2b-5c6d-4e8f-9012-3a4b5c6d7e8f",
    "name": "Bilal Ahmed",
    "phone": "+971557654321",
    "photoUrl": "https://gmsstorage.blob.core.windows.net/drivers/7e1f9a2b.jpg"
  },
  "updatedAt": "2026-08-12T08:44:02Z"
}
```

The document drops out of the drivers' pool query (`status == "new"`) and appears
in this driver's own (`driverId == "7e1f9a2b-..."`) in the same instant.

### 3. Guest aboard, car moving — `status: "in-transit"`

After `start-job` → `arrived` → `start-trip`, with the driver app pushing position:

```json
{
  "status": "in-transit",
  "actualPickupTime": "2026-08-12T09:33:48Z",
  "currentLocation": {
    "latitude": 24.4791,
    "longitude": 54.6048,
    "updatedAt": "2026-08-12T09:36:12Z"
  },
  "updatedAt": "2026-08-12T09:33:48Z"
}
```

Note `currentLocation.updatedAt` is *newer* than the document's top-level
`updatedAt`: the two are written by different parties. The top-level one moves
only on a backend status change; the nested one moves every few seconds. Use
`currentLocation.updatedAt` to decide whether the position is stale enough to grey
out on the guest's map.

### 4. Completed

```json
{
  "status": "completed",
  "actualDropOffTime": "2026-08-12T09:51:30Z",
  "updatedAt": "2026-08-12T09:51:30Z"
}
```

`currentLocation` stays behind at whatever the driver last wrote — the backend
never clears it. Stop rendering the car once `status` is `completed` or
`cancelled`; don't wait for the location to go away.

---

## Lifecycle

`status` walks one way. Every transition is a REST call; the document is rewritten
immediately after each one.

| `status` | Set by | REST call |
|---|---|---|
| `new` | guest creates the request (**`eventId` is required** — see below) | `POST /api/v1/vip-app/transport-requests` |
| `assigned` | driver claims it (`driverId` + `driver{}` appear) | `POST /api/v1/transport-app/jobs/{id}/accept` |
| `in-progress` | driver sets off | `.../jobs/{id}/start-job` |
| `arrived` | driver is at the pickup point | `.../jobs/{id}/arrived` |
| `in-transit` | guest aboard (stamps `actualPickupTime`) | `.../jobs/{id}/start-trip` |
| `completed` | dropped off (stamps `actualDropOffTime`) | `.../jobs/{id}/complete` |
| `cancelled` | guest drops it, only while still `new` | `POST /api/v1/vip-app/transport-requests/{id}/cancel` |

`pending` also exists for admin-created rides that have no driver yet. Admin
"scheduled" rides are **not** mirrored today — only the guest on-demand flow is.

---

## Queries

```js
// Driver — the open pool
db.collection('rideRequests').where('status', '==', 'new')

// Driver — my jobs
db.collection('rideRequests').where('driverId', '==', myDriverPublicId)

// Guest — one ride, live
db.collection('rideRequests').doc(rideId)
```

Add `orderBy('pickupTime')` to either of the first two and Firestore will demand a
composite index; its error message links straight to the create page.

## Driver location write

```js
db.collection('rideRequests').doc(rideId).set({
  currentLocation: {
    latitude: 24.4612,
    longitude: 54.4021,
    updatedAt: firebase.firestore.FieldValue.serverTimestamp(),
  }
}, { merge: true })   // merge is mandatory — without it the driver erases the ride
```

## Security rules

The apps authenticate against the GMS backend, not Firebase, so they hold no
Firebase identity out of the box — `request.auth` is null and every client read
and write is rejected. A Firebase custom token (uid + `role` / `driverId` /
`guestId` claims) has to be minted at login and exchanged via
`signInWithCustomToken` before any of the above works from a client.

```
match /rideRequests/{rideId} {
  allow read: if request.auth != null && (
    request.auth.token.role == 'driver' ||
    request.auth.token.guestId == resource.data.guest.id
  );

  allow update: if request.auth != null
    && request.auth.token.driverId == resource.data.driverId
    && request.resource.data.diff(resource.data).affectedKeys().hasOnly(['currentLocation']);

  allow create, delete: if false;   // backend only
}
```

`hasOnly(['currentLocation'])` is the load-bearing clause: without it, any driver
who can write a position can also write `status: "completed"` or reassign the ride.

---

## Configuration

`appsettings.json` → `Firebase` section, shared with FCM push:

| Key | Meaning |
|---|---|
| `ProjectId` | required; mirroring is disabled without it |
| `ServiceAccountKeyPath` | service-account JSON path (preferred) |
| `ServiceAccountJson` | inline JSON, for env-var secret injection |
| `FirestoreDatabaseId` | blank = `(default)`; set it only for a named database |

No credentials configured => `RideMirror` logs a warning once and no-ops, so a dev
machine without a key still runs.

Source: `Infrastructure/Services/RideMirror.cs`.


## Guest identity: person vs participation

`guest.id` is the **person** (`Guest.PublicId`) — stable across every event they
attend, and what person-level things key off: OTP login, push notifications,
support chat, driver chat.

`eventGuestId` is the **participation** (`EventGuest.PublicId`) — one person on
one event. Every ride belongs to exactly one of these, which is what makes
`eventId`/`eventName` above answerable at all.

The two are different ids and are not interchangeable. In particular, a client
must not send an `eventGuestId` where a guest/person id is expected — the
notification and chat endpoints resolve people, not bookings.

`POST /api/v1/vip-app/transport-requests` therefore **requires `eventId`**. The
server resolves the caller's participation in that event and checks
`allowedServices` on that exact participation; it will not guess an event from
the guest's other bookings. A guest not on the named event gets a 404, and one
without transport self-service on it gets a 403.
