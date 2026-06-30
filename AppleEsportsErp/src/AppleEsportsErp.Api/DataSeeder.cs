using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Domain.Entities;
using AppleEsportsErp.Domain.Enums;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api;

public static class DataSeeder
{
    public static async Task SeedBranchesAsync(AppDbContext db)
    {
        if (await db.Branches.AnyAsync(b => b.Name == "Adajan" || b.Name == "Citylight"))
        {
            return; // Already seeded
        }

        var defaultPassword = BCrypt.Net.BCrypt.HashPassword("12345");
        var adminRoleUserId = (await db.Users.FirstOrDefaultAsync(u => u.Role == "super_admin"))?.Id ?? Guid.Empty;

        if (adminRoleUserId == Guid.Empty)
        {
            await db.Database.ExecuteSqlRawAsync("INSERT INTO users (\"Email\", \"PasswordHash\", \"FullName\", \"Role\", \"Status\", \"CreatedAt\", \"UpdatedAt\") VALUES ('admin@appleesports.com', {0}, 'System Admin', 'super_admin', 'active', NOW(), NOW())", defaultPassword);
            adminRoleUserId = (await db.Users.FirstOrDefaultAsync(u => u.Role == "super_admin"))?.Id ?? Guid.Empty;
        }

        // --- Adajan ---
        var adajan = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Adajan",
            Address = "HG3,4 Samarat Vihar Apt, Opp. Honey Park Apartment, Near Surbhi Dairy, Honey Park Area, Adajan, Surat – 395009",
            Status = BranchStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Branches.Add(adajan);

        var adajanOperators = new[] { "Jigar", "Ankur" };
        var adajanContact = "+91 9909507037";
        foreach (var op in adajanOperators)
        {
            db.Operators.Add(new Operator
            {
                Id = Guid.NewGuid(),
                FullName = op,
                Username = op.ToLower() + "_adajan",
                PasswordHash = defaultPassword,
                MobileNumber = adajanContact,
                BranchId = adajan.Id,
                Status = OperatorStatus.Active,
                CreatedBy = adminRoleUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var adajanPricing = new PricingProfile
        {
            Id = Guid.NewGuid(),
            Name = "PRO COMBAT DESK",
            BaseHourlyRate = 60m,
            BranchId = adajan.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.PricingProfiles.Add(adajanPricing);

        for (int i = 1; i <= 16; i++)
        {
            db.Pcs.Add(new Pc
            {
                Id = Guid.NewGuid(),
                PcNumber = $"ADJ-PC-{i:D2}",
                PcName = $"PRO COMBAT PC {i:D2}",
                BranchId = adajan.Id,
                State = PcState.Idle,
                Zone = "PRO COMBAT DESK",
                PricingProfileId = adajanPricing.Id,
                HardwareNotes = "i5 13th Gen | RTX 4060 Ti | 240Hz",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        // --- Citylight ---
        var citylight = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Citylight",
            Address = "Citylight, Surat", // Placeholder
            Status = BranchStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Branches.Add(citylight);

        var citylightOperators = new[] { "Harshal", "Nazmin" };
        var citylightContact = "+91 9909507036";
        foreach (var op in citylightOperators)
        {
            db.Operators.Add(new Operator
            {
                Id = Guid.NewGuid(),
                FullName = op,
                Username = op.ToLower() + "_citylight",
                PasswordHash = defaultPassword,
                MobileNumber = citylightContact,
                BranchId = citylight.Id,
                Status = OperatorStatus.Active,
                CreatedBy = adminRoleUserId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        var citylightPricing1 = new PricingProfile { Id = Guid.NewGuid(), Name = "CHAMPION ZONE", BaseHourlyRate = 50m, BranchId = citylight.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var citylightPricing2 = new PricingProfile { Id = Guid.NewGuid(), Name = "ELITE WAR ZONE", BaseHourlyRate = 60m, BranchId = citylight.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.PricingProfiles.AddRange(citylightPricing1, citylightPricing2);

        // 35 PCs. Let's do 15 champion, 20 elite
        for (int i = 1; i <= 15; i++)
        {
            db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"CTL-PC-{i:D2}", PcName = $"CHAMPION PC {i:D2}", BranchId = citylight.Id, State = PcState.Idle, Zone = "CHAMPION ZONE", PricingProfileId = citylightPricing1.Id, HardwareNotes = "i5 11th Gen | GTX 1660 Ti | 144Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }
        for (int i = 16; i <= 35; i++)
        {
            db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"CTL-PC-{i:D2}", PcName = $"ELITE PC {i:D2}", BranchId = citylight.Id, State = PcState.Idle, Zone = "ELITE WAR ZONE", PricingProfileId = citylightPricing2.Id, HardwareNotes = "i5 13th Gen | RTX 3060 Ti | 240Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }

        // --- Katargam ---
        var katargam = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Katargam",
            Address = "236,237 Laxmi Enclave 2, Opp. Gajera School, Katargam, Surat – 395004",
            Status = BranchStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Branches.Add(katargam);

        var katargamOperators = new[] { "Karan", "Mayur" };
        var katargamContact = "+91 9909507047";
        foreach (var op in katargamOperators)
        {
            db.Operators.Add(new Operator { Id = Guid.NewGuid(), FullName = op, Username = op.ToLower() + "_katargam", PasswordHash = defaultPassword, MobileNumber = katargamContact, BranchId = katargam.Id, Status = OperatorStatus.Active, CreatedBy = adminRoleUserId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }

        var katargamPricing1 = new PricingProfile { Id = Guid.NewGuid(), Name = "RECRUIT DECK", BaseHourlyRate = 60m, BranchId = katargam.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var katargamPricing2 = new PricingProfile { Id = Guid.NewGuid(), Name = "VETERAN STAND", BaseHourlyRate = 70m, BranchId = katargam.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var katargamPricing3 = new PricingProfile { Id = Guid.NewGuid(), Name = "VIP ELITE HUB", BaseHourlyRate = 80m, BranchId = katargam.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.PricingProfiles.AddRange(katargamPricing1, katargamPricing2, katargamPricing3);

        // 32 PCs
        for (int i = 1; i <= 10; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"KAT-PC-{i:D2}", PcName = $"RECRUIT PC {i:D2}", BranchId = katargam.Id, State = PcState.Idle, Zone = "RECRUIT DECK", PricingProfileId = katargamPricing1.Id, HardwareNotes = "i5 11th Gen | GTX 1660 Ti | 165Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (int i = 11; i <= 21; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"KAT-PC-{i:D2}", PcName = $"VETERAN PC {i:D2}", BranchId = katargam.Id, State = PcState.Idle, Zone = "VETERAN STAND", PricingProfileId = katargamPricing2.Id, HardwareNotes = "i5 13th Gen | RTX 2060 Ti | 240Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (int i = 22; i <= 32; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"KAT-PC-{i:D2}", PcName = $"VIP PC {i:D2}", BranchId = katargam.Id, State = PcState.Idle, Zone = "VIP ELITE HUB", PricingProfileId = katargamPricing3.Id, HardwareNotes = "i7 13th Gen | RTX 3060 Ti Trio | 360Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        // --- Varachha ---
        var varachha = new Branch
        {
            Id = Guid.NewGuid(),
            Name = "Varachha",
            Address = "ELITA SQUARE, 107, VIP Circle To Utran Road, Mota Varachha, Surat – 394105",
            Status = BranchStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Branches.Add(varachha);

        var varachhaOperators = new[] { "Bhavdip", "Darshan" };
        var varachhaContact = "+91 9909507038";
        foreach (var op in varachhaOperators)
        {
            db.Operators.Add(new Operator { Id = Guid.NewGuid(), FullName = op, Username = op.ToLower() + "_varachha", PasswordHash = defaultPassword, MobileNumber = varachhaContact, BranchId = varachha.Id, Status = OperatorStatus.Active, CreatedBy = adminRoleUserId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }

        var varachhaPricing1 = new PricingProfile { Id = Guid.NewGuid(), Name = "TITAN DESK", BaseHourlyRate = 80m, BranchId = varachha.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var varachhaPricing2 = new PricingProfile { Id = Guid.NewGuid(), Name = "GOD-TIER ARENA", BaseHourlyRate = 90m, BranchId = varachha.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var varachhaPricing3 = new PricingProfile { Id = Guid.NewGuid(), Name = "SOFA CLUB COUCH", BaseHourlyRate = 100m, BranchId = varachha.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.PricingProfiles.AddRange(varachhaPricing1, varachhaPricing2, varachhaPricing3);

        for (int i = 1; i <= 10; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"VAR-PC-{i:D2}", PcName = $"TITAN PC {i:D2}", BranchId = varachha.Id, State = PcState.Idle, Zone = "TITAN DESK", PricingProfileId = varachhaPricing1.Id, HardwareNotes = "i7 14th Gen | RTX 5060 Ti Gaming 2X | 240Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (int i = 11; i <= 20; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"VAR-PC-{i:D2}", PcName = $"GOD PC {i:D2}", BranchId = varachha.Id, State = PcState.Idle, Zone = "GOD-TIER ARENA", PricingProfileId = varachhaPricing2.Id, HardwareNotes = "i7 14th Gen | RTX 5060 Ti Gaming 2X | 400Hz", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        for (int i = 1; i <= 3; i++) db.Pcs.Add(new Pc { Id = Guid.NewGuid(), PcNumber = $"VAR-PS5-{i:D2}", PcName = $"PS5 SOFA {i:D2}", BranchId = varachha.Id, State = PcState.Idle, Zone = "SOFA CLUB COUCH", PricingProfileId = varachhaPricing3.Id, HardwareNotes = "PlayStation 5 | 4K OLED", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });

        // --- Seed Default Member ---
        if (!await db.Members.AnyAsync(m => m.Username == "testmember"))
        {
            db.Members.Add(new Member
            {
                Id = Guid.NewGuid(),
                MemberNumber = "MEM-0001",
                FullName = "Test Member",
                MobileNumber = "9876543210",
                Email = "testmember@appleesports.com",
                Username = "testmember",
                PasswordHash = defaultPassword,
                GamingBalance = 500m,
                FoodBalance = 200m,
                TotalPoints = 150,
                JoinDate = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                HomeBranchId = adajan.Id
            });
        }

        await db.SaveChangesAsync();
    }
}
