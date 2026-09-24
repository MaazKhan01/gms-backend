-- Required: every table here carries filtered indexes ([IsDeleted] = 0), and
-- SQL Server refuses inserts against those unless QUOTED_IDENTIFIER is ON.
-- SSMS defaults it ON; sqlcmd does not, so it is set explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ============================================================================
   DMS seed — part 1 of 2: roles, admin user, reference data, services.
   Adapted from GMS-Seeding.txt. Run part 1 then part 2 (menus + grants).

   Idempotent where it can cheaply be: every insert is guarded by NOT EXISTS,
   so re-running tops up rather than duplicating.
   ========================================================================= */

/* ── Roles ───────────────────────────────────────────────────────────────
   PortalAccess   — may this role sign in to the admin portal?
   IsDelegateRole — is this offered as a delegate's role on a mission?
                    (EventGuest.MissionRoleId points here; there is no separate
                    delegate-type lookup.)
   Head of Delegation is BOTH: they travel with the delegation and sign in.     */
INSERT INTO dbo.Roles (Name, Code, Description, CreatedAt, IsDeleted, PublicId, PortalAccess, IsDelegateRole)
SELECT v.Name, v.Code, v.Description, SYSUTCDATETIME(), 0, NEWID(), v.PortalAccess, v.IsDelegateRole
FROM (VALUES
    ('Administrator',      'admin',              'Full system access',                                                              1, 0),
    ('Protocol Officer',   'protocol-officer',   'Logs invitations, creates missions, owns host communication, publishes reports',  1, 0),
    ('Mission Coordinator','mission-coordinator','Assembles the roster, records logistics, runs readiness and on-ground ops',       1, 0),
    ('Department Head',    'department-head',    'Nominates staff from their own department',                                       1, 0),
    ('HR Administrator',   'hr-administrator',   'Verifies passport, grade, visa and insurance records for the roster',             1, 0),
    ('Head of Delegation', 'head-of-delegation', 'Field decisions, protocol order and review of the combined mission report',       1, 1),
    ('Member',             'member',             'Delegation member travelling on the mission',                                     0, 1),
    ('Support Staff',      'support-staff',      'Support staff travelling with the delegation',                                    0, 1),
    ('Viewer',             'viewer',             'Read-only access across the modules granted to it',                               1, 0),
    ('Driver',             'driver',             'Ground-transport driver with vehicle and license details on file',                0, 0),
    ('Guest',              'guest',              'VIP app account, auto-provisioned alongside its Guest profile',                   0, 1)
) AS v(Name, Code, Description, PortalAccess, IsDelegateRole)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Code = v.Code AND r.IsDeleted = 0);


/* ── Admin user ──────────────────────────────────────────────────────────
   PasswordHash is carried over from the GMS seed — change it after first
   sign-in, or replace the hash below before running.                         */
INSERT INTO dbo.Users (UserName, Email, RoleId, FirstName, LastName, Phone, IsActive,
                       CreatedAt, IsDeleted, PublicId, PasswordHash)
SELECT 'admin', 'admin@dms.local', r.Id, 'System', 'Administrator', NULL, 1,
       SYSUTCDATETIME(), 0, NEWID(), '$2a$11$xPbo3UX1Xc2S1RsPbXOUxuwJbCsj0LBMdsVdY9BSm/WfFAjyMsbV.'
FROM dbo.Roles r
WHERE r.Code = 'admin' AND r.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.Email = 'admin@dms.local' AND u.IsDeleted = 0);


/* ── Departments ─────────────────────────────────────────────────────────
   New in DMS. Guest.DepartmentId points here; a Department Head nominates
   only from their own department.                                            */
INSERT INTO dbo.Departments (Name, NameAr, CreatedAt, IsDeleted, PublicId)
SELECT v.Name, v.NameAr, SYSUTCDATETIME(), 0, NEWID()
FROM (VALUES
    (N'Protocol Office',  N'مكتب المراسم'),
    (N'Logistics',        N'الخدمات اللوجستية'),
    (N'Media & Press',    N'الإعلام والصحافة'),
    (N'Security',         N'الأمن'),
    (N'Guest Relations',  N'علاقات الضيوف')
) AS v(Name, NameAr)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Departments d WHERE d.Name = v.Name AND d.IsDeleted = 0);


/* ── Mission types ───────────────────────────────────────────────────────
   Stored in EventTypes (an Event IS a mission). Replaces the GMS list
   (Conference / Sports / Exhibition / Food Festival) with the mission types
   from the workflow document.                                                */
INSERT INTO dbo.EventTypes (Name, CreatedAt, IsDeleted, PublicId)
SELECT v.Name, SYSUTCDATETIME(), 0, NEWID()
FROM (VALUES ('Conference'), ('Championship'), ('Official Visit'), ('Other')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.EventTypes t WHERE t.Name = v.Name AND t.IsDeleted = 0);


/* ── Flight classes ──────────────────────────────────────────────────────  */
INSERT INTO dbo.FlightClasses (Name, CreatedAt, IsDeleted, PublicId)
SELECT v.Name, SYSUTCDATETIME(), 0, NEWID()
FROM (VALUES ('Economy'), ('Business'), ('First Class')) AS v(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.FlightClasses f WHERE f.Name = v.Name AND f.IsDeleted = 0);


/* ── Services ────────────────────────────────────────────────────────────
   The four DMS services. 'arrivals-departures' from the GMS seed is dropped:
   travel dates now come off the flight bookings, and Visa takes its place as
   the fourth tracked service.                                                */

-- flight
INSERT INTO Services (PublicId, Code, Name, NameAr, Description, Icon, SortOrder, IsActive, FormSchemaJson, CreatedAt, IsDeleted)
SELECT NEWID(), 'flight', N'Flight', N'الطيران', N'Inbound and outbound flights booked for the delegate.', 'planeTakeoff', 1, 1, NULL, SYSUTCDATETIME(), 0
WHERE NOT EXISTS (SELECT 1 FROM Services WHERE Code = 'flight' AND IsDeleted = 0);

-- accommodation
INSERT INTO Services (PublicId, Code, Name, NameAr, Description, Icon, SortOrder, IsActive, FormSchemaJson, CreatedAt, IsDeleted)
SELECT NEWID(), 'accommodation', N'Accommodation', N'الإقامة', N'Hotel, room type, check-in and check-out.', 'hotel', 2, 1, NULL, SYSUTCDATETIME(), 0
WHERE NOT EXISTS (SELECT 1 FROM Services WHERE Code = 'accommodation' AND IsDeleted = 0);

-- transport
INSERT INTO Services (PublicId, Code, Name, NameAr, Description, Icon, SortOrder, IsActive, FormSchemaJson, CreatedAt, IsDeleted)
SELECT NEWID(), 'transport', N'Transport', N'النقل', N'Ground transfers: vehicle, driver, pickup and drop-off.', 'car', 3, 1, NULL, SYSUTCDATETIME(), 0
WHERE NOT EXISTS (SELECT 1 FROM Services WHERE Code = 'transport' AND IsDeleted = 0);

-- visa (replaces arrivals-departures; dynamic fields, editable from the portal)
INSERT INTO Services (PublicId, Code, Name, NameAr, Description, Icon, SortOrder, IsActive, FormSchemaJson, CreatedAt, IsDeleted)
SELECT NEWID(), 'visa', N'Visa', N'التأشيرة',
       N'Visa application and issued document for the destination.',
       'doc', 4, 1,
N'{"sections":[
  {"key":"application","label":"Application","labelAr":"الطلب","fields":[
    {"key":"visaType","label":"Visa type","labelAr":"نوع التأشيرة","type":"text","required":false,"maxLength":60},
    {"key":"appliedOn","label":"Applied on","labelAr":"تاريخ التقديم","type":"date","required":false},
    {"key":"referenceNo","label":"Reference no.","labelAr":"الرقم المرجعي","type":"text","required":false,"maxLength":60}
  ]},
  {"key":"outcome","label":"Outcome","labelAr":"النتيجة","fields":[
    {"key":"issuedOn","label":"Issued on","labelAr":"تاريخ الإصدار","type":"date","required":false,"afterField":"appliedOn"},
    {"key":"expiresOn","label":"Expires on","labelAr":"تاريخ الانتهاء","type":"date","required":false,"afterField":"issuedOn"},
    {"key":"document","label":"Visa document","labelAr":"وثيقة التأشيرة","type":"file","required":false},
    {"key":"notes","label":"Notes","labelAr":"ملاحظات","type":"textarea","required":false,"maxLength":500}
  ]}
]}',
       SYSUTCDATETIME(), 0
WHERE NOT EXISTS (SELECT 1 FROM Services WHERE Code = 'visa' AND IsDeleted = 0);


/* Optional: attach all four to a service level (replace level code 'vip'). */
INSERT INTO ServiceLevelServices (PublicId, ServiceLevelId, ServiceId, SortOrder, CreatedAt, IsDeleted)
SELECT NEWID(), sl.Id, s.Id, s.SortOrder, SYSUTCDATETIME(), 0
FROM ServiceLevels sl
JOIN Services s ON s.Code IN ('flight','accommodation','transport','visa') AND s.IsDeleted = 0
WHERE sl.Code = 'vip' AND sl.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM ServiceLevelServices x
                  WHERE x.ServiceLevelId = sl.Id AND x.ServiceId = s.Id AND x.IsDeleted = 0);


/* ── Nationalities ───────────────────────────────────────────────────────
   Carried over from the GMS seed with one repair: every English name ending
   in 'n' had lost that letter there (Afghanista, Japa, Spai, Swede, Yeme ...).
   25 names restored; names not ending in 'n' were unaffected.                */
insert into dbo.Nationalities ( PublicId, Name, NameAr, Code, Flag)
values  ( '47C5A000-D307-4765-8439-B61BAFAA9D4B', 'Afghanistan', 'أفغانستان', 'AF', '🇦🇫'),
        ( 'E29DE15D-87CF-4799-B234-AF0C89380510', 'Albania', 'ألبانيا', 'AL', '🇦🇱'),
        ('D6FE7693-CC35-4694-B215-60DD7F564ED6', 'Algeria', 'الجزائر', 'DZ', '🇩🇿'),
        ('59336104-0DC0-48EE-87FF-D00E85250FE5', 'Andorra', 'أندورا', 'AD', '🇦🇩'),
        ('112945CF-C564-4E3C-9554-3A46F8757633', 'Angola', 'أنغولا', 'AO', '🇦🇴'),
        ('5D1F5EE7-9C95-4C60-959F-F607C5C10A83', 'Argentina', 'الأرجنتين', 'AR', '🇦🇷'),
        ('6365A200-3A3A-4ABA-96BB-7C2F277A884F', 'Armenia', 'أرمينيا', 'AM', '🇦🇲'),
        ('51B47450-7E69-45A7-BA56-716405567490', 'Australia', 'أستراليا', 'AU', '🇦🇺'),
        ('EFB8C96B-EE48-4112-B4CA-3FEFD2A33AE3', 'Austria', 'النمسا', 'AT', '🇦🇹'),
        ('8649B04F-3082-4437-8F77-75148A0C3CD8', 'Azerbaijan', 'أذربيجان', 'AZ', '🇦🇿'),
        ('C759D4AD-B91E-426A-BEFC-59F473FD132D', 'Bahamas', 'جزر البهاما', 'BS', '🇧🇸'),
        ('A84FFFBA-2C1A-4839-AB42-8DFC861F00CF', 'Bahrain', 'البحرين', 'BH', '🇧🇭'),
        ('3B7C4419-2F08-451E-BD07-6854B424B196', 'Bangladesh', 'بنغلاديش', 'BD', '🇧🇩'),
        ('6EB6A6F2-E621-4B16-864F-766661F8F5D2', 'Belarus', 'بيلاروسيا', 'BY', '🇧🇾'),
        ('1F4DD7FE-D5D3-4F0B-80FF-0ABC5FCC815D', 'Belgium', 'بلجيكا', 'BE', '🇧🇪'),
        ('D504BB6E-DD49-4D48-B55C-F686E590C865', 'Belize', 'بليز', 'BZ', '🇧🇿'),
        ('2B5EAF19-E0AB-4691-A672-8EC15C33A56A', 'Benin', 'بنين', 'BJ', '🇧🇯'),
        ('0DEFC74F-CC37-44B8-864D-309F18D8AFC4', 'Bhutan', 'بوتان', 'BT', '🇧🇹'),
        ('A65F8648-340E-4AE2-9D55-F67B779A058C', 'Bolivia', 'بوليفيا', 'BO', '🇧🇴'),
        ('838FEE4C-3FBF-4D8B-83FA-99F13131CA2A', 'Bosnia and Herzegovina', 'البوسنة والهرسك', 'BA', '🇧🇦'),
        ('E03F7D4D-0811-415D-885E-9F3076652C2B', 'Botswana', 'بوتسوانا', 'BW', '🇧🇼'),
        ('FBB51179-E01C-42EF-9D1B-8B31C396D17E', 'Brazil', 'البرازيل', 'BR', '🇧🇷'),
        ('28B5F867-9022-438D-A725-B6591BF127BF', 'Brunei', 'بروناي', 'BN', '🇧🇳'),
        ('B14197FC-0D4F-435C-88E6-D69E5C933A4B', 'Bulgaria', 'بلغاريا', 'BG', '🇧🇬'),
        ('B7D5F8B8-5C51-40B2-BA82-79CAF06950A9', 'Burkina Faso', 'بوركينا فاسو', 'BF', '🇧🇫'),
        ('7F2DDCBE-1DEB-4EF5-A708-5C9E4900B5D4', 'Burundi', 'بوروندي', 'BI', '🇧🇮'),
        ('4D4FB0E8-2E33-4FCD-A4EC-AFE1B6D2ED26', 'Cambodia', 'كمبوديا', 'KH', '🇰🇭'),
        ('2051FE9F-2A9C-4DBE-96DE-49697F7C2CE9', 'Cameroon', 'الكاميرون', 'CM', '🇨🇲'),
        ('61112CD8-A6CC-4301-B056-6D7514F1B7DB', 'Canada', 'كندا', 'CA', '🇨🇦'),
        ('A38F928F-B089-46F8-BFC7-CDF7F8A8F00C', 'Cape Verde', 'الرأس الأخضر', 'CV', '🇨🇻'),
        ('9639772F-2598-471D-A34F-D3D138EA7C72', 'Central African Republic', 'جمهورية أفريقيا الوسطى', 'CF', '🇨🇫'),
        ('D3C2AC28-C236-4369-B42E-4A9C25A2E7F1', 'Chad', 'تشاد', 'TD', '🇹🇩'),
        ('AADDAABB-40B8-4F6E-B538-0BFA1A6D7D7C', 'Chile', 'تشيلي', 'CL', '🇨🇱'),
        ('06A1B6D5-44E7-48E9-98E0-E7313DAAB8B7', 'China', 'الصين', 'CN', '🇨🇳'),
        ('A9D8E282-4EB1-4F39-A8CD-E3316D64072B', 'Colombia', 'كولومبيا', 'CO', '🇨🇴'),
        ('65D6A78B-5027-47E9-B36E-2BA223FC5B16', 'Comoros', 'جزر القمر', 'KM', '🇰🇲'),
        ('48623C8A-F5B2-4411-955F-F69D798431DE', 'Congo', 'الكونغو', 'CG', '🇨🇬'),
        ('CE303A9B-F5CB-42DE-BA34-6225905E01B8', 'Costa Rica', 'كوستا ريكا', 'CR', '🇨🇷'),
        ('CE3EF13B-E3BB-449F-AD08-A3D37D4C69B3', 'Croatia', 'كرواتيا', 'HR', '🇭🇷'),
        ('9015ECEB-1217-407D-A2CE-14886C67004B', 'Cuba', 'كوبا', 'CU', '🇨🇺'),
        ('8F1F23A5-4DD4-4F1F-B256-5A14A1E99D3D', 'Cyprus', 'قبرص', 'CY', '🇨🇾'),
        ('8BD466FF-9945-4ADB-834D-0D15CE87C8F2', 'Czech Republic', 'جمهورية التشيك', 'CZ', '🇨🇿'),
        ('2B76B2D8-7F1C-4140-9832-7D7EE00AE2D8', 'Denmark', 'الدنمارك', 'DK', '🇩🇰'),
        ('378BA506-DE29-4A19-A071-50E5E99E8085', 'Djibouti', 'جيبوتي', 'DJ', '🇩🇯'),
        ('6256ABE2-E82C-4482-AA75-9F3560EE7AE9', 'Dominican Republic', 'جمهورية الدومينيكان', 'DO', '🇩🇴'),
        ('2313B1B0-110C-4744-80F6-3D63A196290D', 'Ecuador', 'الإكوادور', 'EC', '🇪🇨'),
        ('3443B31D-D8B3-4FB1-BE6D-83FD48DA7110', 'Egypt', 'مصر', 'EG', '🇪🇬'),
        ('A56B9448-E05E-41E0-BC3C-D0F09E561903', 'El Salvador', 'السلفادور', 'SV', '🇸🇻'),
        ('A0F359CF-CE78-496C-8F88-4C98EAC51BB5', 'Equatorial Guinea', 'غينيا الاستوائية', 'GQ', '🇬🇶'),
        ('4899BBA7-BEC9-4612-8C7C-31A1D2F64F4A', 'Eritrea', 'إريتريا', 'ER', '🇪🇷'),
        ('CFE1CBCA-33F7-4487-83AA-2A49887B6C0B', 'Estonia', 'إستونيا', 'EE', '🇪🇪'),
        ('17082ABD-9369-4B2E-BDA8-FFC164B5D1D1', 'Ethiopia', 'إثيوبيا', 'ET', '🇪🇹'),
        ('D3B76413-5430-41FE-91B3-227B24D59812', 'Fiji', 'فيجي', 'FJ', '🇫🇯'),
        ('E5310A9D-217E-407C-AD86-B82030480982', 'Finland', 'فنلندا', 'FI', '🇫🇮'),
        ('8660E9D1-8747-4781-BAC7-A6EB81DACD0A', 'France', 'فرنسا', 'FR', '🇫🇷'),
        ('00033147-4341-4529-9537-01BD8798A898', 'Gabon', 'الغابون', 'GA', '🇬🇦'),
        ('C4692AFF-BBD1-4A40-BEBB-4FDD1C1E40E8', 'Gambia', 'غامبيا', 'GM', '🇬🇲'),
        ('9FFFD823-5E41-4CA2-919C-61DEE91F8791', 'Georgia', 'جورجيا', 'GE', '🇬🇪'),
        ('55A80A2C-022D-4519-AF2F-8B755DC5F520', 'Germany', 'ألمانيا', 'DE', '🇩🇪'),
        ('C8545EEA-0841-410C-B23A-CEE00522E260', 'Ghana', 'غانا', 'GH', '🇬🇭'),
        ('C76912BE-416F-4274-9BE5-6BA9ABB948C7', 'Greece', 'اليونان', 'GR', '🇬🇷'),
        ('D2A59ED9-783B-42E3-AAC0-9265941D5EE5', 'Guatemala', 'غواتيمالا', 'GT', '🇬🇹'),
        ('10B8DB7A-67C3-400C-8A12-B136241E08A2', 'Guinea', 'غينيا', 'GN', '🇬🇳'),
        ('E270F34F-80C6-4D22-B2B3-B75FCB846FCB', 'Guinea-Bissau', 'غينيا بيساو', 'GW', '🇬🇼'),
        ('4290F197-DE19-4354-A2BD-A212DB4767D5', 'Guyana', 'غيانا', 'GY', '🇬🇾'),
        ('D22CEADA-79A9-471A-BFDF-DF690A390D65', 'Haiti', 'هايتي', 'HT', '🇭🇹'),
        ('78B2C428-C94E-4768-AC59-E0AD4967E06C', 'Honduras', 'هندوراس', 'HN', '🇭🇳'),
        ('DEE877AD-38FC-4BD6-BD77-51D6C8ADAC66', 'Hungary', 'المجر', 'HU', '🇭🇺'),
        ('742C29E7-E663-4C57-9A04-6F7ED47FDF51', 'Iceland', 'أيسلندا', 'IS', '🇮🇸'),
        ('AE1032AB-D043-401D-BC76-D532B57F4435', 'India', 'الهند', 'IN', '🇮🇳'),
        ('81FF9D8A-B92F-4663-9FCB-85A872D57CDB', 'Indonesia', 'إندونيسيا', 'ID', '🇮🇩'),
        ('6904FACE-DEFE-4049-A923-5EFF84F717CA', 'Iran', 'إيران', 'IR', '🇮🇷'),
        ('265193C2-EE46-4ACA-9DE8-48B871810CE8', 'Iraq', 'العراق', 'IQ', '🇮🇶'),
        ('644FD7A1-CB17-4735-B771-CE0CAA0C61B6', 'Ireland', 'أيرلندا', 'IE', '🇮🇪'),
        ('24946346-94F6-420F-9176-140B452AD9E7', 'Israel', 'إسرائيل', 'IL', '🇮🇱'),
        ('E1372540-3B4C-490D-980E-4D842009BAB5', 'Italy', 'إيطاليا', 'IT', '🇮🇹'),
        ('C50C56B2-270A-4949-B8B7-E0A9DD408199', 'Jamaica', 'جامايكا', 'JM', '🇯🇲'),
        ('0CBAA5B6-E336-4BCD-8ED0-305F47E1988C', 'Japan', 'اليابان', 'JP', '🇯🇵'),
        ('2F75B48E-3C97-49D7-A7BE-FD7231BD38AF', 'Jordan', 'الأردن', 'JO', '🇯🇴'),
        ('A7A9268A-43F1-4FF1-A7BD-40E465735934', 'Kazakhstan', 'كازاخستان', 'KZ', '🇰🇿'),
        ('CBF10CA2-58FC-43C0-92AF-65F20F593CA8', 'Kenya', 'كينيا', 'KE', '🇰🇪'),
        ('A64BFCE2-70CB-4209-B61A-4A9ADC88F7CC', 'Kuwait', 'الكويت', 'KW', '🇰🇼'),
        ('96C42182-B702-45B8-8349-FE4FF6363B4B', 'Kyrgyzstan', 'قيرغيزستان', 'KG', '🇰🇬'),
        ('6739EAC6-E47E-4350-8D66-7BC1C0A00018', 'Laos', 'لاوس', 'LA', '🇱🇦'),
        ('14B32E36-D3A1-4FDB-B139-1F15179ED036', 'Latvia', 'لاتفيا', 'LV', '🇱🇻'),
        ('C951DD3B-2274-47A8-AD98-4A27349623F9', 'Lebanon', 'لبنان', 'LB', '🇱🇧'),
        ('40EE3380-61B1-4A53-B15D-4B99238A0D81', 'Lesotho', 'ليسوتو', 'LS', '🇱🇸'),
        ('4AE89810-AC68-4DA5-9CD4-1642FDA60F49', 'Liberia', 'ليبيريا', 'LR', '🇱🇷'),
        ('16738CBC-5772-493D-BA41-E1FFDC77E916', 'Libya', 'ليبيا', 'LY', '🇱🇾'),
        ('9790D1F8-900C-478F-AF5E-7156C0E9DC4E', 'Liechtenstein', 'ليختنشتاين', 'LI', '🇱🇮'),
        ('A7233E32-9CC7-492B-A80C-FCF0386693EF', 'Lithuania', 'ليتوانيا', 'LT', '🇱🇹'),
        ('C0BE78BB-0828-4FE5-8509-575AC6F0E818', 'Luxembourg', 'لوكسمبورغ', 'LU', '🇱🇺'),
        ('A0668583-C53D-4E83-B5E6-9DF8A9F0D074', 'Madagascar', 'مدغشقر', 'MG', '🇲🇬'),
        ('3C17C86B-C70E-43B7-962D-77CDA8E0FBA6', 'Malawi', 'ملاوي', 'MW', '🇲🇼'),
        ('8F75E70A-73D0-44BC-A549-098C2ED7BB36', 'Malaysia', 'ماليزيا', 'MY', '🇲🇾'),
        ('EA89BB33-5FA5-461D-A897-0DA8FE64BBC6', 'Maldives', 'جزر المالديف', 'MV', '🇲🇻'),
        ('C21C31B3-3104-43BD-BF95-4FC87F81DE33', 'Mali', 'مالي', 'ML', '🇲🇱'),
        ('73392E16-E0FC-4C73-8959-2A6206358E3B', 'Malta', 'مالطا', 'MT', '🇲🇹'),
        ('AF9C7AA5-697F-4986-9EBB-AE5F62D531B9', 'Mauritania', 'موريتانيا', 'MR', '🇲🇷'),
        ('2D172DAC-9FA1-49CA-81EC-A0A5FCB01F9E', 'Mauritius', 'موريشيوس', 'MU', '🇲🇺'),
        ('BAE1E090-ABC9-42F3-B61D-EAA518B4DC61', 'Mexico', 'المكسيك', 'MX', '🇲🇽'),
        ('7CD62305-1F94-4D00-8D3A-DF212C40D7B5', 'Moldova', 'مولدوفا', 'MD', '🇲🇩'),
        ('EA584AB8-5398-4852-8B57-78BB6B1A6B46', 'Monaco', 'موناكو', 'MC', '🇲🇨'),
        ('AAE558AF-AD9E-47EA-B7EE-6AF728A90248', 'Mongolia', 'منغوليا', 'MN', '🇲🇳'),
        ('1CBB2127-56F9-440A-BA44-75BC513A50C0', 'Montenegro', 'الجبل الأسود', 'ME', '🇲🇪'),
        ('B01BEB92-9ED7-4110-9F07-5DF9BFA7BFA0', 'Morocco', 'المغرب', 'MA', '🇲🇦'),
        ('E4980145-413D-49B6-8E26-24FB3AC618D3', 'Mozambique', 'موزمبيق', 'MZ', '🇲🇿'),
        ('E031D6F6-6D94-4D80-9BC4-85EC790D2A48', 'Myanmar', 'ميانمار', 'MM', '🇲🇲'),
        ('DBF0B855-A953-4DCB-892B-0AF8018DDB84', 'Namibia', 'ناميبيا', 'NA', '🇳🇦'),
        ('4ABAF787-3F40-4A8F-9962-63CC2E500F18', 'Nepal', 'نيبال', 'NP', '🇳🇵'),
        ('324E3806-07D1-4746-BB59-C715B7A75DF6', 'Netherlands', 'هولندا', 'NL', '🇳🇱'),
        ('0EEF966D-4781-4F8C-8FC1-D98CF918E2FE', 'New Zealand', 'نيوزيلندا', 'NZ', '🇳🇿'),
        ('C07CD198-5E05-459F-904A-61896346AB58', 'Nicaragua', 'نيكاراغوا', 'NI', '🇳🇮'),
        ('B692A1AD-8366-4B64-9AD1-7AC2B0B8C563', 'Niger', 'النيجر', 'NE', '🇳🇪'),
        ('4A73FADA-DB42-4D85-B293-67566F03EDC3', 'Nigeria', 'نيجيريا', 'NG', '🇳🇬'),
        ('04E4D4BF-56FC-4E7C-BF56-423871DF91E6', 'North Korea', 'كوريا الشمالية', 'KP', '🇰🇵'),
        ('E18A57C2-E402-4F60-B00E-46BEECEB8CD9', 'North Macedonia', 'مقدونيا الشمالية', 'MK', '🇲🇰'),
        ('3133F4FF-7B97-45E1-9693-E2257029CB1A', 'Norway', 'النرويج', 'NO', '🇳🇴'),
        ('895CD516-B636-47CE-9478-B459407C7304', 'Oman', 'عُمان', 'OM', '🇴🇲'),
        ('B281F95D-CEF6-4180-A7BE-14A841ACBE09', 'Pakistan', 'باكستان', 'PK', '🇵🇰'),
        ('F3A6FF5E-21B8-4B68-AC08-22AE468B9503', 'Palestine', 'فلسطين', 'PS', '🇵🇸'),
        ('26B2ADF1-BDEA-4095-9419-B72DEA684089', 'Panama', 'بنما', 'PA', '🇵🇦'),
        ('7CFB44C9-1ABF-4F98-8790-7E199F3E01D2', 'Papua New Guinea', 'بابوا غينيا الجديدة', 'PG', '🇵🇬'),
        ('4BBC258B-35CC-4B8F-8D63-D10296957386', 'Paraguay', 'باراغواي', 'PY', '🇵🇾'),
        ('8487EC87-801D-42D1-8023-3428D39122C3', 'Peru', 'بيرو', 'PE', '🇵🇪'),
        ('60647DC8-CC11-48F2-9777-D006DD95E7DC', 'Philippines', 'الفلبين', 'PH', '🇵🇭'),
        ('79C33F2D-7110-451E-8FEE-44D197857C5A', 'Poland', 'بولندا', 'PL', '🇵🇱'),
        ('DDBA5D65-BD06-49D3-9E0D-AE7AAF989606', 'Portugal', 'البرتغال', 'PT', '🇵🇹'),
        ('DC3298BF-9D24-4A21-A39E-5C81EFD69364', 'Qatar', 'قطر', 'QA', '🇶🇦'),
        ('19F7E986-4EA5-402E-A472-3F3AC8268E50', 'Romania', 'رومانيا', 'RO', '🇷🇴'),
        ('302E1371-12EB-494F-A22C-4BE013207DFC', 'Russia', 'روسيا', 'RU', '🇷🇺'),
        ('417DF272-AAD8-4F4C-8E40-EF09187577F3', 'Rwanda', 'رواندا', 'RW', '🇷🇼'),
        ('ED7A20FE-6411-4FDD-9579-65516E9DA9EF', 'Saudi Arabia', 'المملكة العربية السعودية', 'SA', '🇸🇦'),
        ('1E9407F8-AFE6-447C-BEFD-BB4324E44CEB', 'Senegal', 'السنغال', 'SN', '🇸🇳'),
        ('B0D352EB-FDDA-42D4-A847-6CEF2FBCFAAB', 'Serbia', 'صربيا', 'RS', '🇷🇸'),
        ('ED5BF514-9DC9-42A6-95A0-81468ABACE0E', 'Sierra Leone', 'سيراليون', 'SL', '🇸🇱'),
        ('CC88953E-A712-4A9F-AD6D-A710D3CBD9F6', 'Singapore', 'سنغافورة', 'SG', '🇸🇬'),
        ('8CB84B86-D10F-44ED-898B-7331C3F0DDC5', 'Slovakia', 'سلوفاكيا', 'SK', '🇸🇰'),
        ('FE9201E9-9B34-4B52-9CA8-AABFE626C8F4', 'Slovenia', 'سلوفينيا', 'SI', '🇸🇮'),
        ('69827374-1742-43CE-884E-8653C8253C22', 'Somalia', 'الصومال', 'SO', '🇸🇴'),
        ('DA6324C2-3124-43B2-B3BB-30A92EDDEE9A', 'South Africa', 'جنوب أفريقيا', 'ZA', '🇿🇦'),
        ('1D5D209E-C368-4EA2-9788-99B7EE0EBD75', 'South Korea', 'كوريا الجنوبية', 'KR', '🇰🇷'),
        ('213D0F6C-1367-4637-8EEB-4D447E50B03B', 'South Sudan', 'جنوب السودان', 'SS', '🇸🇸'),
        ('08A95927-90F8-4EF7-AC3F-492F81B9CD85', 'Spain', 'إسبانيا', 'ES', '🇪🇸'),
        ('985D452E-6798-4BC8-B8D6-D601EACD662A', 'Sri Lanka', 'سريلانكا', 'LK', '🇱🇰'),
        ('FD3DA2E1-BCE0-4C16-81F1-6EE503E5C84C', 'Sudan', 'السودان', 'SD', '🇸🇩'),
        ('B09F99FB-619E-4DA6-8E26-0E4CC6254211', 'Sweden', 'السويد', 'SE', '🇸🇪'),
        ('B438BBF6-A718-43DC-91CC-630A336C2679', 'Switzerland', 'سويسرا', 'CH', '🇨🇭'),
        ('DB4BC16D-578A-4DAF-85DA-B51E0FCC1D01', 'Syria', 'سوريا', 'SY', '🇸🇾'),
        ('5B683939-7F64-4486-9D3D-0E9B24AA6878', 'Taiwan', 'تايوان', 'TW', '🇹🇼'),
        ('0A9BFAAE-19BF-4635-81E2-75C5E06D536F', 'Tajikistan', 'طاجيكستان', 'TJ', '🇹🇯'),
        ('C107D8E6-3C3C-4B80-A2F5-9A25C028328A', 'Tanzania', 'تنزانيا', 'TZ', '🇹🇿'),
        ('235A10C8-7591-41AB-975A-9C9DD238F17B', 'Thailand', 'تايلاند', 'TH', '🇹🇭'),
        ('73479D64-372E-49DE-A0E4-1CBD3D3B5853', 'Togo', 'توغو', 'TG', '🇹🇬'),
        ('E422E94A-5FB6-4CD6-9C39-F7443225BAF2', 'Trinidad and Tobago', 'ترينيداد وتوباغو', 'TT', '🇹🇹'),
        ('3A934EFC-A9FF-4D42-BD74-BAF6A7409ADF', 'Tunisia', 'تونس', 'TN', '🇹🇳'),
        ('D4681F09-B4B7-47B8-9592-DC7D427AE20C', 'Turkey', 'تركيا', 'TR', '🇹🇷'),
        ('2B5C02F4-818A-400C-A696-9E6F0E264753', 'Turkmenistan', 'تركمانستان', 'TM', '🇹🇲'),
        ('8F8FA2B3-8932-4A93-8161-C4E857BD1DDE', 'Uganda', 'أوغندا', 'UG', '🇺🇬'),
        ('103014BC-4B34-4EF2-8F97-6483C4684604', 'Ukraine', 'أوكرانيا', 'UA', '🇺🇦'),
        ('FED6C3F8-DB8C-4A80-A423-70EBB87BB807', 'United Arab Emirates', 'الإمارات العربية المتحدة', 'AE', '🇦🇪'),
        ('E54DB3D6-081D-4E25-BDE9-F700B23127E2', 'United Kingdom', 'المملكة المتحدة', 'GB', '🇬🇧'),
        ('28172370-72B8-4AB5-B700-7F5934B83D69', 'United States', 'الولايات المتحدة', 'US', '🇺🇸'),
        ('443313AD-453A-4809-BC0B-1BDA37B25984', 'Uruguay', 'أوروغواي', 'UY', '🇺🇾'),
        ('36875D2D-469B-4F5B-A3FA-ADF72D2791E6', 'Uzbekistan', 'أوزبكستان', 'UZ', '🇺🇿'),
        ('7F8B4D5D-1BA5-4324-80AA-3E6A769DB49D', 'Venezuela', 'فنزويلا', 'VE', '🇻🇪'),
        ('5ECCC977-CAE8-4946-9F1A-2D4D005C6080', 'Vietnam', 'فيتنام', 'VN', '🇻🇳'),
        ('BC0DBD56-C09B-4E52-9654-47739E1CF728', 'Yemen', 'اليمن', 'YE', '🇾🇪'),
        ('A82040B7-2319-4BEE-902B-1C0F3616194E', 'Zambia', 'زامبيا', 'ZM', '🇿🇲'),
        ('89CE48CF-0DCB-458A-8F44-7F21852C9058', 'Zimbabwe', 'زيمبابوي', 'ZW', '🇿🇼');


