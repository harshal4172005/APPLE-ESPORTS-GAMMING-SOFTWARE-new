using Microsoft.EntityFrameworkCore;
using AppleEsportsErp.Infrastructure.Data;

namespace AppleEsportsErp.Api;

public static class DbUpdater
{
    public static void UpdateSchema(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sql = @"
CREATE TABLE IF NOT EXISTS ""PricingProfiles"" (
    ""Id"" uuid NOT NULL DEFAULT uuid_generate_v4(),
    ""Name"" character varying(100) NOT NULL,
    ""BaseHourlyRate"" numeric NOT NULL,
    ""BranchId"" uuid NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT true,
    ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
    ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT ""PK_PricingProfiles"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_PricingProfiles_branches_BranchId"" FOREIGN KEY (""BranchId"") REFERENCES branches (id) ON DELETE CASCADE
);

ALTER TABLE pcs
ADD COLUMN IF NOT EXISTS ""PcName"" character varying(100),
ADD COLUMN IF NOT EXISTS ""Zone"" character varying(50),
ADD COLUMN IF NOT EXISTS ""PricingProfileId"" uuid,
ADD COLUMN IF NOT EXISTS ""HardwareNotes"" text,
ADD COLUMN IF NOT EXISTS ""IsActive"" boolean NOT NULL DEFAULT true,
ADD COLUMN IF NOT EXISTS ""IsDeleted"" boolean NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_Pcs_PricingProfiles_PricingProfileId'
    ) THEN
        ALTER TABLE pcs
        ADD CONSTRAINT ""FK_Pcs_PricingProfiles_PricingProfileId"" FOREIGN KEY (""PricingProfileId"") REFERENCES ""PricingProfiles"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;";

        db.Database.ExecuteSqlRaw(sql);

        // Member login credentials (username + password)
        db.Database.ExecuteSqlRaw(@"
ALTER TABLE members
ADD COLUMN IF NOT EXISTS ""Username"" character varying(50),
ADD COLUMN IF NOT EXISTS ""PasswordHash"" character varying(255),
ADD COLUMN IF NOT EXISTS ""GamingBalance"" numeric(10, 2) NOT NULL DEFAULT 0.0,
ADD COLUMN IF NOT EXISTS ""FoodBalance"" numeric(10, 2) NOT NULL DEFAULT 0.0;

CREATE UNIQUE INDEX IF NOT EXISTS ""IX_members_Username""
    ON members (""Username"")
    WHERE ""Username"" IS NOT NULL;

ALTER TABLE wallet_transactions
ADD COLUMN IF NOT EXISTS ""TargetWallet"" character varying(20) NOT NULL DEFAULT 'Gaming';

ALTER TABLE reservations
ADD COLUMN IF NOT EXISTS ""AdvanceDeposit"" numeric(18,2) NOT NULL DEFAULT 0.0;

ALTER TABLE inventory
ADD COLUMN IF NOT EXISTS ""SoldQty"" integer NOT NULL DEFAULT 0;

ALTER TABLE operators
ADD COLUMN IF NOT EXISTS ""IsGlobalAdmin"" boolean NOT NULL DEFAULT false;

ALTER TABLE bills
ADD COLUMN IF NOT EXISTS ""IsDeferred"" boolean NOT NULL DEFAULT false;
");

        // Execute data seeding for branches, PCs, operators, etc.
        DataSeeder.SeedBranchesAsync(db).GetAwaiter().GetResult();
    }
}
