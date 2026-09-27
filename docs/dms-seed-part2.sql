-- Required: every table here carries filtered indexes ([IsDeleted] = 0), and
-- SQL Server refuses inserts against those unless QUOTED_IDENTIFIER is ON.
-- SSMS defaults it ON; sqlcmd does not, so it is set explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ============================================================================
   DMS seed — part 2 of 2: the menu tree, its parentage, and the admin grant.
   Run AFTER part 1 (it needs the Roles row for 'admin').

   One Permissions row = one menu or submenu. Path NULL = a grouping header with
   no page of its own. Codes MUST match Core/Common/PermissionCodes.cs — the API
   gates on them, so a mismatch is a silent 403.

   Sections and order follow the DMS prototype (DMS-v2_1.html). The 60 old
   action-style rows from the GMS seed ("Users.Create", "Guests.View", ... which
   were already soft-deleted tombstones there) are NOT carried over — this is a
   fresh database and they would only be dead rows.
   ========================================================================= */

INSERT INTO dbo.Permissions (Code, Name, NameAr, Description, Icon, [Path], SortOrder, IsActive, CreatedAt, IsDeleted, PublicId)
SELECT v.Code, v.Name, v.NameAr, v.Description, v.Icon, v.[Path], v.SortOrder, 1, SYSUTCDATETIME(), 0, NEWID()
FROM (VALUES
    -- ── Sections (no page of their own) ─────────────────────────────────
    ('mission',              N'Missions',              N'المهام',                NULL, NULL, NULL,                  100),
    ('delegation',           N'Delegation Assembly',   N'تشكيل الوفد',           NULL, NULL, NULL,                  200),
    ('host-communication',   N'Host Communication',    N'التواصل مع المضيف',     NULL, NULL, NULL,                  300),
    ('event',                N'Event',                 N'الفعالية',              NULL, NULL, NULL,                  400),
    ('pre-departure',        N'Pre-Departure',         N'ما قبل السفر',          NULL, NULL, NULL,                  500),
    ('active-mission',       N'Active Mission',        N'المهمة الجارية',        NULL, NULL, NULL,                  600),
    ('reports-close',        N'Reports & Close',       N'التقارير والإغلاق',     NULL, NULL, NULL,                  700),
    ('onsite',               N'On-Site',               N'في الموقع',             NULL, NULL, NULL,                  800),
    ('venue-management',     N'Venue Management',      N'إدارة الأماكن',         NULL, NULL, NULL,                  900),
    ('fleet',                N'Fleet',                 N'الأسطول',               NULL, NULL, NULL,                 1000),
    ('accommodation',        N'Accommodation',         N'الإقامة',               NULL, NULL, NULL,                 1100),
    ('admin',                N'Administration',        N'الإدارة',               NULL, NULL, NULL,                 1200),
    ('user-management',      N'User Management',       N'إدارة المستخدمين',      NULL, NULL, NULL,                 1300),

    -- ── MISSIONS ────────────────────────────────────────────────────────
    ('dashboard',            N'Dashboard',             N'لوحة المعلومات',        N'Overview of the active mission',              'dashboard',    '/dashboard',             110),
    ('external-invitations', N'External Invitations',  N'الدعوات الخارجية',      N'Invitations logged from host organizations',  'invitation',   '/external-invitations',  120),
    -- Code stays 'events' (EventsController / the Event entity serve it); the
    -- LABEL is Missions, the same way 'guests' is labelled Delegates.
    ('events',               N'Missions',              N'المهام',                N'Missions created from invitations',           'meetings',     '/missions',              130),

    -- ── DELEGATION ASSEMBLY ─────────────────────────────────────────────
    ('guests',               N'Delegates',             N'المندوبون',             N'All delegates registered for this mission',   'guests',       '/guests',                210),
    ('nominations',          N'Nominations',           N'الترشيحات',             N'Nominate staff and assemble the roster',      'protocol',     '/nominations',           220),
    ('hr-verification',      N'HR Verification',       N'تدقيق الموارد البشرية', N'Confirm passport, grade, visa & insurance',   'check',        '/hr-verification',       230),

    -- ── HOST COMMUNICATION ──────────────────────────────────────────────
    ('nomination-letter',    N'Nomination Letter',     N'خطاب الترشيح',          N'Generate, send and track the letter',         'invitation',   '/nomination-letter',     310),

    -- ── EVENT ───────────────────────────────────────────────────────────
    ('services',             N'Services',              N'الخدمات',               N'Flights, accommodation, transport & visa',    'travel',       '/travel',                410),
    ('support-chat',         N'Support',               N'الدعم',                 N'Conversations with delegates via the app',    'message',      '/support-chat',          430),

    -- ── PRE-DEPARTURE ───────────────────────────────────────────────────
    ('readiness',            N'Readiness',             N'الجاهزية',              N'Per-delegate readiness checklist',            'check',        '/readiness',             510),

    -- ── ACTIVE MISSION ──────────────────────────────────────────────────
    ('on-mission-ops',       N'On-Mission Ops',        N'عمليات المهمة',         N'Headcount, gathering notices, reassignments', 'message',      '/on-mission-ops',        610),
    ('incidents',            N'Help Requests',         N'طلبات المساعدة',        N'Delegate help requests and escalation',       'alertTriangle','/incidents',             620),
    ('head-of-delegation',   N'Head of Delegation',    N'رئيس الوفد',            N'Roster, protocol order & field decisions',    'protocol',     '/head-of-delegation',    630),

    -- ── REPORTS & CLOSE ─────────────────────────────────────────────────
    ('post-mission-reports', N'Post-Mission Reports',  N'تقارير ما بعد المهمة',  N'Track and nudge delegate trip reports',       'doc',          '/post-mission-reports',  710),
    ('combined-report',      N'Combined Report',       N'التقرير المجمع',        N'Assemble, review, approve & publish',         'reports',      '/combined-report',       720),

    -- ── ON-SITE ─────────────────────────────────────────────────────────
    ('accreditation',        N'Accreditation',         N'الاعتماد',              N'Badge issuance and status',                   'badge',        '/accreditation',         810),
    ('seating',              N'Seating',               N'الجلوس',                N'Floor plan and table assignments',            'seating',      '/seating',               820),
    ('meetings',             N'Meetings',              N'الاجتماعات',            N'Scheduled bilaterals and briefings',          'meetings',     '/meetings',              830),

    -- ── VENUE MANAGEMENT ────────────────────────────────────────────────
    ('venue-config',         N'Venue Config',          N'إعداد المكان',          N'Drag-and-drop layout designer',               'venue',        '/venue-config',          910),
    ('venues',               N'Venues',                N'الأماكن',               N'Venue directory',                             'venues',       '/venues',                920),

    -- ── FLEET ───────────────────────────────────────────────────────────
    ('vehicles',             N'Vehicles',              N'المركبات',              N'Fleet vehicle directory',                     'car',          '/vehicles',             1010),
    ('fleet-providers',      N'Fleet Providers',       N'موردو النقل',           N'Transport vendors',                           'venue',        '/fleet-providers',      1020),
    ('fleet-bookings',       N'Bookings',              N'الحجوزات',              N'Vehicle bookings and driver assignments',     'meetings',     '/fleet-bookings',       1030),
    ('lookup-vehicle-types', N'Vehicle Types',         N'أنواع المركبات',        N'Reference data',                              'reports',      '/lookups/vehicle-types',1040),

    -- ── ACCOMMODATION ───────────────────────────────────────────────────
    ('room-inventory',       N'Inventory',             N'المخزون',               N'Room blocks by hotel',                        'hotel',        '/room-inventory',       1110),
    ('lookup-hotels',        N'Hotels',                N'الفنادق',               N'Reference data',                              'reports',      '/lookups/hotels',       1120),
    ('lookup-room-types',    N'Room Types',            N'أنواع الغرف',           N'Reference data',                              'reports',      '/lookups/room-types',   1130),

    -- ── ADMINISTRATION ──────────────────────────────────────────────────
    ('template-builder',     N'Template Builder',      N'منشئ القوالب',          N'Invitation templates and performance',        'invitation',   '/invitations',          1210),
    ('guest-overview',       N'Delegate Overview',     N'نظرة عامة',             N'Cross-mission delegate analytics',            'reports',      '/guest-overview',       1220),
    ('organizations',        N'Organizations',         N'المؤسسات',              N'Institutions and their delegations',          'venue',        '/organizations',        1230),
    ('service-levels',       N'Service Levels',        N'مستويات الخدمة',        N'Delegate tiers and bundled services',         'badge',        '/service-levels',       1240),
    ('manage-services',      N'Manage Services',       N'إدارة الخدمات',         N'Service catalogue configuration',             'star',         '/services',             1250),
    ('lookups',              N'Lookups',               N'البيانات المرجعية',     N'Reference data used across the portal',       'reports',      NULL,                    1260),

    -- ── ADMINISTRATION › Lookups ────────────────────────────────────────
    ('lookup-flight-classes',N'Flight Classes',        N'درجات الرحلة',          N'Reference data',                              'reports',      '/lookups/flight-classes',1261),
    ('lookup-airports',      N'Airports',              N'المطارات',              N'Reference data',                              'reports',      '/lookups/airports',      1262),
    ('lookup-locations',     N'Locations',             N'المواقع',               N'Reference data',                              'reports',      '/lookups/locations',     1263),
    ('lookup-event-types',   N'Mission Types',         N'أنواع المهام',          N'Reference data',                              'reports',      '/lookups/event-types',   1264),
    ('lookup-venue-types',   N'Venue Types',           N'أنواع الأماكن',         N'Reference data',                              'reports',      '/lookups/venue-types',   1265),
    ('lookup-element-types', N'Venue Element',         N'عنصر القاعة',           N'Reference data',                              'reports',      '/lookups/element-types', 1266),
    ('lookup-departments',   N'Departments',           N'الأقسام',               N'Reference data',                              'reports',      '/lookups/departments',   1267),

    -- ── USER MANAGEMENT ─────────────────────────────────────────────────
    ('users',                N'Users',                 N'المستخدمون',            N'Portal user accounts',                        'guests',       '/users',                1310),
    ('roles',                N'Roles',                 N'الأدوار',               N'Roles and delegate types',                    'protocol',     '/roles',                1320),
    ('role-access',          N'Role Access',           N'صلاحيات الأدوار',       N'Read/write permissions per role, per menu',   'protocol',     '/role-access',          1340)
) AS v(Code, Name, NameAr, Description, Icon, [Path], SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Code = v.Code AND p.IsDeleted = 0);


/* ── Parentage (resolved by Code — identity ids are generated) ─────────── */
UPDATE c
SET c.[ParentId] = p.[Id]
FROM dbo.Permissions c
JOIN (VALUES
    ('dashboard',            'mission'),
    ('external-invitations', 'mission'),
    ('events',               'mission'),

    ('guests',               'delegation'),
    ('nominations',          'delegation'),
    ('hr-verification',      'delegation'),

    ('nomination-letter',    'host-communication'),

    ('services',             'event'),
    ('support-chat',         'event'),

    ('readiness',            'pre-departure'),

    ('on-mission-ops',       'active-mission'),
    ('incidents',            'active-mission'),
    ('head-of-delegation',   'active-mission'),

    ('post-mission-reports', 'reports-close'),
    ('combined-report',      'reports-close'),

    ('accreditation',        'onsite'),
    ('seating',              'onsite'),
    ('meetings',             'onsite'),

    ('venue-config',         'venue-management'),
    ('venues',               'venue-management'),

    ('vehicles',             'fleet'),
    ('fleet-providers',      'fleet'),
    ('fleet-bookings',       'fleet'),
    ('lookup-vehicle-types', 'fleet'),

    ('room-inventory',       'accommodation'),
    ('lookup-hotels',        'accommodation'),
    ('lookup-room-types',    'accommodation'),

    ('template-builder',     'admin'),
    ('guest-overview',       'admin'),
    ('organizations',        'admin'),
    ('service-levels',       'admin'),
    ('manage-services',      'admin'),
    ('lookups',              'admin'),

    ('lookup-flight-classes','lookups'),
    ('lookup-airports',      'lookups'),
    ('lookup-locations',     'lookups'),
    ('lookup-event-types',   'lookups'),
    ('lookup-venue-types',   'lookups'),
    ('lookup-element-types', 'lookups'),
    ('lookup-departments',   'lookups'),

    ('users',                'user-management'),
    ('roles',                'user-management'),
    ('role-access',          'user-management')
) AS m([ChildCode], [ParentCode]) ON m.[ChildCode] = c.[Code]
JOIN dbo.Permissions p ON p.[Code] = m.[ParentCode] AND p.IsDeleted = 0
WHERE c.IsDeleted = 0;


/* ── Grant every module to Administrator ─────────────────────────────────
   RoleId is hardcoded to 1. That is correct here because part 1 inserts
   Administrator first into an empty Roles table, so it takes identity 1.
   The EXISTS guard below makes that assumption explicit: if row 1 is ever not
   the admin role, this grants nothing rather than handing every module to the
   wrong role.                                                                 */
INSERT INTO dbo.RolePermissions (RoleId, PermissionId, CreatedAt, IsDeleted, PublicId, CanRead, CanWrite)
SELECT 1, p.Id, SYSUTCDATETIME(), 0, NEWID(), 1, 1
FROM dbo.Permissions p
WHERE p.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Id = 1 AND r.Code = 'admin' AND r.IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp
                  WHERE rp.RoleId = 1 AND rp.PermissionId = p.Id AND rp.IsDeleted = 0);
