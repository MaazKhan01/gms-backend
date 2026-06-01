-- ============================================
-- AppConfiguration Seed Data
-- ============================================
-- This script inserts initial configuration values for the QBF application.
-- Run this after creating the AppConfiguration table via migration.

-- Note: Replace NEWID() with actual GUIDs if your system doesn't support it

-- 1. Slots Generation Days (for GenerateSlotsForNext30DaysAsync)
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'SlotsGenerationDays',
    '30',
    'int',
    'Number of days to generate court availability slots in advance',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- 2. Booking Cancellation Cutoff Hours
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'BookingCancellationCutoffMinutes',
    '24',
    'int',
    'Minimum hours before booking start time to allow cancellation',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- 3. Additional useful configurations (optional)

-- Default booking duration
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'DefaultBookingDurationMinutes',
    '60',
    'int',
    'Default booking duration in minutes if not specified',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- Maximum bookings per user per day
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'MaxBookingsPerUserPerDay',
    '3',
    'int',
    'Maximum number of bookings a user can make in a single day',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- System maintenance mode
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'MaintenanceMode',
    'false',
    'bool',
    'Enable maintenance mode to prevent new bookings',
    'System',
    1,
    GETUTCDATE(),
    0
);

-- Booking confirmation required
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'BookingRequiresAdminConfirmation',
    'false',
    'bool',
    'Whether bookings require admin confirmation before being active',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- Auto-cancel unpaid bookings after hours
INSERT INTO AppConfiguration (Id, [Key], [Value], DataType, [Description], Category, IsActive, CreatedAt, IsDeleted)
VALUES (
    NEWID(),
    'AutoCancelUnpaidBookingHours',
    '48',
    'int',
    'Automatically cancel unpaid bookings after this many hours',
    'Booking',
    1,
    GETUTCDATE(),
    0
);

-- ============================================
-- Verification Query
-- ============================================
-- Run this to verify the data was inserted correctly
SELECT
    [Key],
    [Value],
    DataType,
    [Description],
    Category,
    IsActive
FROM AppConfiguration
WHERE IsDeleted = 0
ORDER BY Category, [Key];
