using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Common;
using Core.Constants;
using DomainPersistence.Entities;

namespace Infrastructure.Data;

/// <summary>
/// Idempotent startup seeder. Safe to run on every boot:
///   1. Applies pending EF migrations (creates the database if missing).
///   2. Syncs the Permission catalog from <see cref="PermissionCodes"/>.
///   3. Ensures the built-in roles (admin / user) exist.
///   4. Grants every permission to the admin role.
///   5. Creates a default admin user (credentials from the "Seed" config section).
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        ApplicationDBContext db,
        IConfiguration config,
        ILogger logger = null,
        CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        await SyncPermissionsAsync(db, ct);
        var adminRole = await EnsureRoleAsync(db, Roles.ADMIN, "Administrator", "Full system access", ct);
        await EnsureRoleAsync(db, Roles.USER, "User", "Standard user", ct);

        // Persist permissions + roles first so the DB assigns their identity int Ids.
        // Everything below (RolePermissions, users) needs those generated int Ids.
        await db.SaveChangesAsync(ct);

        await GrantAllPermissionsAsync(db, adminRole, ct);
        await SeedDefinedRolesAsync(db, ct);
        await SeedNationalitiesAsync(db, ct);
        await SeedVenueRefDataAsync(db, ct);
        await EnsureAdminUserAsync(db, config, adminRole, logger, ct);

        await db.SaveChangesAsync(ct);
        logger?.LogInformation("Data seeding completed.");
    }

    // 1+2. Ensure a Permission row exists for every code declared in PermissionCodes.
    private static async Task SyncPermissionsAsync(ApplicationDBContext db, CancellationToken ct)
    {
        var codes = typeof(PermissionCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct()
            .ToList();

        var existing = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var existingSet = new HashSet<string>(existing);

        foreach (var code in codes.Where(c => !existingSet.Contains(c)))
        {
            var parts = code.Split('.', 2);
            var module = parts.Length > 0 ? parts[0] : code;
            var action = parts.Length > 1 ? parts[1] : code;
            db.Permissions.Add(new Permission
            {
                Code = code,
                Module = module,
                Name = $"{module} · {action}",
                Description = $"Allows {action} in {module}",
            });
        }
    }

    private static async Task<Role> EnsureRoleAsync(
        ApplicationDBContext db, string code, string name, string description, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code, ct);
        if (role == null)
        {
            role = new Role { Code = code, Name = name, Description = description };
            db.Roles.Add(role);
        }
        return role;
    }

    // 4. Make sure the admin role is linked to every permission.
    private static async Task GrantAllPermissionsAsync(ApplicationDBContext db, Role adminRole, CancellationToken ct)
    {
        // Permissions + roles are already persisted (see SeedAsync), so their int Ids are set.
        var permissionIds = await db.Permissions.Select(p => p.Id).ToListAsync(ct);

        var linkedIds = await db.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);
        var linkedSet = new HashSet<int>(linkedIds);

        foreach (var permissionId in permissionIds.Where(id => !linkedSet.Contains(id)))
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                PermissionId = permissionId,
            });
        }
    }

    // Build a code -> int id map from persisted permissions.
    private static Dictionary<string, int> PermissionMap(ApplicationDBContext db)
        => db.Permissions.AsNoTracking().ToDictionary(p => p.Code, p => p.Id);

    // Sync built-in GMS roles: create missing roles AND replace permissions for
    // existing ones so RoleDefinitions.cs is always the source of truth.
    // The admin role is managed separately (GrantAllPermissionsAsync) and is skipped here.
    private static async Task SeedDefinedRolesAsync(ApplicationDBContext db, CancellationToken ct)
    {
        var permMap = PermissionMap(db);

        foreach (var def in RoleDefinitions.All)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == def.Code, ct);
            if (role == null)
            {
                role = new Role { Code = def.Code, Name = def.Name, Description = def.Description };
                db.Roles.Add(role);
                // Persist so the DB assigns role.Id before we wire its RolePermissions.
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Sync name/description in case it changed.
                role.Name = def.Name;
                role.Description = def.Description;
            }

            // Diff: only remove stale links and add missing ones — never re-insert existing rows.
            var existing = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync(ct);
            var currentPermIds = existing.Select(rp => rp.PermissionId).ToHashSet();

            var desiredPermIds = def.Permissions.Distinct()
                .Where(code => permMap.ContainsKey(code))
                .Select(code => permMap[code])
                .ToHashSet();

            // Remove permissions no longer in the definition.
            db.RolePermissions.RemoveRange(existing.Where(rp => !desiredPermIds.Contains(rp.PermissionId)));

            // Add only permissions not already linked.
            foreach (var pid in desiredPermIds.Where(id => !currentPermIds.Contains(id)))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = pid });
        }
    }

    // 5. Create the default admin user if no account with that email exists.
    private static async Task EnsureAdminUserAsync(
        ApplicationDBContext db, IConfiguration config, Role adminRole, ILogger logger, CancellationToken ct)
    {
        var email = config["Seed:AdminEmail"] ?? "admin@gms.local";
        var password = config["Seed:AdminPassword"] ?? "Admin@123!";
        var userName = config["Seed:AdminUserName"] ?? "admin";

        var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct);
        if (exists) return;

        db.Users.Add(new User
        {
            UserName = userName,
            Email = email,
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true,
            RoleId = adminRole.Id,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        });

        logger?.LogWarning("Seeded admin user '{Email}'. Change the seeded password after first login.", email);
    }

    private static async Task SeedNationalitiesAsync(ApplicationDBContext db, CancellationToken ct)
    {
        if (await db.Nationalities.AnyAsync(ct)) return;

        var countries = new[]
        {
            ("Afghanistan","أفغانستان","AF","🇦🇫"),("Albania","ألبانيا","AL","🇦🇱"),("Algeria","الجزائر","DZ","🇩🇿"),
            ("Andorra","أندورا","AD","🇦🇩"),("Angola","أنغولا","AO","🇦🇴"),("Argentina","الأرجنتين","AR","🇦🇷"),
            ("Armenia","أرمينيا","AM","🇦🇲"),("Australia","أستراليا","AU","🇦🇺"),("Austria","النمسا","AT","🇦🇹"),
            ("Azerbaijan","أذربيجان","AZ","🇦🇿"),("Bahamas","جزر البهاما","BS","🇧🇸"),("Bahrain","البحرين","BH","🇧🇭"),
            ("Bangladesh","بنغلاديش","BD","🇧🇩"),("Belarus","بيلاروسيا","BY","🇧🇾"),("Belgium","بلجيكا","BE","🇧🇪"),
            ("Belize","بليز","BZ","🇧🇿"),("Benin","بنين","BJ","🇧🇯"),("Bhutan","بوتان","BT","🇧🇹"),
            ("Bolivia","بوليفيا","BO","🇧🇴"),("Bosnia and Herzegovina","البوسنة والهرسك","BA","🇧🇦"),
            ("Botswana","بوتسوانا","BW","🇧🇼"),("Brazil","البرازيل","BR","🇧🇷"),("Brunei","بروناي","BN","🇧🇳"),
            ("Bulgaria","بلغاريا","BG","🇧🇬"),("Burkina Faso","بوركينا فاسو","BF","🇧🇫"),("Burundi","بوروندي","BI","🇧🇮"),
            ("Cambodia","كمبوديا","KH","🇰🇭"),("Cameroon","الكاميرون","CM","🇨🇲"),("Canada","كندا","CA","🇨🇦"),
            ("Cape Verde","الرأس الأخضر","CV","🇨🇻"),("Central African Republic","جمهورية أفريقيا الوسطى","CF","🇨🇫"),
            ("Chad","تشاد","TD","🇹🇩"),("Chile","تشيلي","CL","🇨🇱"),("China","الصين","CN","🇨🇳"),
            ("Colombia","كولومبيا","CO","🇨🇴"),("Comoros","جزر القمر","KM","🇰🇲"),("Congo","الكونغو","CG","🇨🇬"),
            ("Costa Rica","كوستا ريكا","CR","🇨🇷"),("Croatia","كرواتيا","HR","🇭🇷"),("Cuba","كوبا","CU","🇨🇺"),
            ("Cyprus","قبرص","CY","🇨🇾"),("Czech Republic","جمهورية التشيك","CZ","🇨🇿"),("Denmark","الدنمارك","DK","🇩🇰"),
            ("Djibouti","جيبوتي","DJ","🇩🇯"),("Dominican Republic","جمهورية الدومينيكان","DO","🇩🇴"),
            ("Ecuador","الإكوادور","EC","🇪🇨"),("Egypt","مصر","EG","🇪🇬"),("El Salvador","السلفادور","SV","🇸🇻"),
            ("Equatorial Guinea","غينيا الاستوائية","GQ","🇬🇶"),("Eritrea","إريتريا","ER","🇪🇷"),
            ("Estonia","إستونيا","EE","🇪🇪"),("Ethiopia","إثيوبيا","ET","🇪🇹"),("Fiji","فيجي","FJ","🇫🇯"),
            ("Finland","فنلندا","FI","🇫🇮"),("France","فرنسا","FR","🇫🇷"),("Gabon","الغابون","GA","🇬🇦"),
            ("Gambia","غامبيا","GM","🇬🇲"),("Georgia","جورجيا","GE","🇬🇪"),("Germany","ألمانيا","DE","🇩🇪"),
            ("Ghana","غانا","GH","🇬🇭"),("Greece","اليونان","GR","🇬🇷"),("Guatemala","غواتيمالا","GT","🇬🇹"),
            ("Guinea","غينيا","GN","🇬🇳"),("Guinea-Bissau","غينيا بيساو","GW","🇬🇼"),("Guyana","غيانا","GY","🇬🇾"),
            ("Haiti","هايتي","HT","🇭🇹"),("Honduras","هندوراس","HN","🇭🇳"),("Hungary","المجر","HU","🇭🇺"),
            ("Iceland","أيسلندا","IS","🇮🇸"),("India","الهند","IN","🇮🇳"),("Indonesia","إندونيسيا","ID","🇮🇩"),
            ("Iran","إيران","IR","🇮🇷"),("Iraq","العراق","IQ","🇮🇶"),("Ireland","أيرلندا","IE","🇮🇪"),
            ("Israel","إسرائيل","IL","🇮🇱"),("Italy","إيطاليا","IT","🇮🇹"),("Jamaica","جامايكا","JM","🇯🇲"),
            ("Japan","اليابان","JP","🇯🇵"),("Jordan","الأردن","JO","🇯🇴"),("Kazakhstan","كازاخستان","KZ","🇰🇿"),
            ("Kenya","كينيا","KE","🇰🇪"),("Kuwait","الكويت","KW","🇰🇼"),("Kyrgyzstan","قيرغيزستان","KG","🇰🇬"),
            ("Laos","لاوس","LA","🇱🇦"),("Latvia","لاتفيا","LV","🇱🇻"),("Lebanon","لبنان","LB","🇱🇧"),
            ("Lesotho","ليسوتو","LS","🇱🇸"),("Liberia","ليبيريا","LR","🇱🇷"),("Libya","ليبيا","LY","🇱🇾"),
            ("Liechtenstein","ليختنشتاين","LI","🇱🇮"),("Lithuania","ليتوانيا","LT","🇱🇹"),("Luxembourg","لوكسمبورغ","LU","🇱🇺"),
            ("Madagascar","مدغشقر","MG","🇲🇬"),("Malawi","ملاوي","MW","🇲🇼"),("Malaysia","ماليزيا","MY","🇲🇾"),
            ("Maldives","جزر المالديف","MV","🇲🇻"),("Mali","مالي","ML","🇲🇱"),("Malta","مالطا","MT","🇲🇹"),
            ("Mauritania","موريتانيا","MR","🇲🇷"),("Mauritius","موريشيوس","MU","🇲🇺"),("Mexico","المكسيك","MX","🇲🇽"),
            ("Moldova","مولدوفا","MD","🇲🇩"),("Monaco","موناكو","MC","🇲🇨"),("Mongolia","منغوليا","MN","🇲🇳"),
            ("Montenegro","الجبل الأسود","ME","🇲🇪"),("Morocco","المغرب","MA","🇲🇦"),("Mozambique","موزمبيق","MZ","🇲🇿"),
            ("Myanmar","ميانمار","MM","🇲🇲"),("Namibia","ناميبيا","NA","🇳🇦"),("Nepal","نيبال","NP","🇳🇵"),
            ("Netherlands","هولندا","NL","🇳🇱"),("New Zealand","نيوزيلندا","NZ","🇳🇿"),("Nicaragua","نيكاراغوا","NI","🇳🇮"),
            ("Niger","النيجر","NE","🇳🇪"),("Nigeria","نيجيريا","NG","🇳🇬"),("North Korea","كوريا الشمالية","KP","🇰🇵"),
            ("North Macedonia","مقدونيا الشمالية","MK","🇲🇰"),("Norway","النرويج","NO","🇳🇴"),("Oman","عُمان","OM","🇴🇲"),
            ("Pakistan","باكستان","PK","🇵🇰"),("Palestine","فلسطين","PS","🇵🇸"),("Panama","بنما","PA","🇵🇦"),
            ("Papua New Guinea","بابوا غينيا الجديدة","PG","🇵🇬"),("Paraguay","باراغواي","PY","🇵🇾"),("Peru","بيرو","PE","🇵🇪"),
            ("Philippines","الفلبين","PH","🇵🇭"),("Poland","بولندا","PL","🇵🇱"),("Portugal","البرتغال","PT","🇵🇹"),
            ("Qatar","قطر","QA","🇶🇦"),("Romania","رومانيا","RO","🇷🇴"),("Russia","روسيا","RU","🇷🇺"),
            ("Rwanda","رواندا","RW","🇷🇼"),("Saudi Arabia","المملكة العربية السعودية","SA","🇸🇦"),("Senegal","السنغال","SN","🇸🇳"),
            ("Serbia","صربيا","RS","🇷🇸"),("Sierra Leone","سيراليون","SL","🇸🇱"),("Singapore","سنغافورة","SG","🇸🇬"),
            ("Slovakia","سلوفاكيا","SK","🇸🇰"),("Slovenia","سلوفينيا","SI","🇸🇮"),("Somalia","الصومال","SO","🇸🇴"),
            ("South Africa","جنوب أفريقيا","ZA","🇿🇦"),("South Korea","كوريا الجنوبية","KR","🇰🇷"),("South Sudan","جنوب السودان","SS","🇸🇸"),
            ("Spain","إسبانيا","ES","🇪🇸"),("Sri Lanka","سريلانكا","LK","🇱🇰"),("Sudan","السودان","SD","🇸🇩"),
            ("Sweden","السويد","SE","🇸🇪"),("Switzerland","سويسرا","CH","🇨🇭"),("Syria","سوريا","SY","🇸🇾"),
            ("Taiwan","تايوان","TW","🇹🇼"),("Tajikistan","طاجيكستان","TJ","🇹🇯"),("Tanzania","تنزانيا","TZ","🇹🇿"),
            ("Thailand","تايلاند","TH","🇹🇭"),("Togo","توغو","TG","🇹🇬"),("Trinidad and Tobago","ترينيداد وتوباغو","TT","🇹🇹"),
            ("Tunisia","تونس","TN","🇹🇳"),("Turkey","تركيا","TR","🇹🇷"),("Turkmenistan","تركمانستان","TM","🇹🇲"),
            ("Uganda","أوغندا","UG","🇺🇬"),("Ukraine","أوكرانيا","UA","🇺🇦"),("United Arab Emirates","الإمارات العربية المتحدة","AE","🇦🇪"),
            ("United Kingdom","المملكة المتحدة","GB","🇬🇧"),("United States","الولايات المتحدة","US","🇺🇸"),
            ("Uruguay","أوروغواي","UY","🇺🇾"),("Uzbekistan","أوزبكستان","UZ","🇺🇿"),("Venezuela","فنزويلا","VE","🇻🇪"),
            ("Vietnam","فيتنام","VN","🇻🇳"),("Yemen","اليمن","YE","🇾🇪"),("Zambia","زامبيا","ZM","🇿🇲"),
            ("Zimbabwe","زيمبابوي","ZW","🇿🇼")
        };

        foreach (var (name, nameAr, code, flag) in countries)
        {
            db.Nationalities.Add(new Nationality
            {
                Name = name,
                NameAr = nameAr,
                Code = code,
                Flag = flag
            });
        }
    }

    // Seed venue reference data (dedicated tables). Only seeded when the table is
    // empty — the venue editor needs its element-type palette to function.
    private static async Task SeedVenueRefDataAsync(ApplicationDBContext db, CancellationToken ct)
    {
        if (!await db.VenueTypes.AnyAsync(ct))
        {
            var venueTypes = new (string name, string nameAr)[]
            {
                ("Auditorium",      "قاعة محاضرات"),
                ("Ballroom",        "قاعة احتفالات"),
                ("Conference Hall", "قاعة مؤتمرات"),
                ("Meeting Room",    "غرفة اجتماعات"),
                ("Stadium",         "استاد"),
                ("Outdoor",         "مساحة خارجية"),
            };
            foreach (var (name, nameAr) in venueTypes)
                db.VenueTypes.Add(new VenueType { Name = name, NameAr = nameAr });
        }

        if (!await db.ElementTypes.AnyAsync(ct))
        {
            var elementTypes = new (string code, string name, string nameAr)[]
            {
                ("round",   "Round Table",       "طاولة دائرية"),
                ("rect",    "Rectangular Table", "طاولة مستطيلة"),
                ("stadium", "Stadium Block",     "كتلة مدرجات"),
                ("stage",   "Stage",             "منصة"),
                ("pitch",   "Pitch Area",        "منطقة ملعب"),
            };
            foreach (var (code, name, nameAr) in elementTypes)
                db.ElementTypes.Add(new ElementType { Code = code, Name = name, NameAr = nameAr });
        }
    }
}
