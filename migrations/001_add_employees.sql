-- ============================================================
-- Migration 001: Add Employees Table
-- Apple Esports ERP — HR Module
-- Note: Uses PascalCase columns to match EF Core conventions
-- Safe to run multiple times (IF NOT EXISTS guard)
-- ============================================================

CREATE TABLE IF NOT EXISTS employees (
    "Id"                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "BranchId"                UUID NOT NULL REFERENCES branches("Id") ON DELETE RESTRICT,
    "EmployeeNumber"          TEXT NOT NULL,

    -- Personal Information
    "FullName"                TEXT NOT NULL,
    "Gender"                  TEXT,
    "DateOfBirth"             DATE,
    "Nationality"             TEXT DEFAULT 'Indian',
    "MaritalStatus"           TEXT,

    -- Contact Information
    "PermanentAddress"        TEXT,
    "CurrentAddress"          TEXT,
    "Phone"                   TEXT,
    "Email"                   TEXT,

    -- Emergency Contact
    "EmergencyName"           TEXT,
    "EmergencyRelationship"   TEXT,
    "EmergencyPhone"          TEXT,
    "EmergencyEmail"          TEXT,
    "EmergencyAddress"        TEXT,

    -- Job Information
    "PositionTitle"           TEXT,
    "Department"              TEXT,
    "Supervisor"              TEXT,
    "StartDate"               DATE,

    -- Banking Information
    "BankName"                TEXT,
    "AccountNumber"           TEXT,
    "AccountHolderName"       TEXT,
    "BankBranch"              TEXT,

    -- Reference
    "RefName"                 TEXT,
    "RefRelationship"         TEXT,
    "RefPhone"                TEXT,
    "RefAddress"              TEXT,

    -- System Fields
    "Status"                  TEXT NOT NULL DEFAULT 'Active',
    "SubmittedBy"             UUID REFERENCES operators("Id") ON DELETE SET NULL,
    "CreatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"               TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Unique constraint on EmployeeNumber
DO $$ BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'employees_employeenumber_unique'
  ) THEN
    ALTER TABLE employees ADD CONSTRAINT employees_employeenumber_unique UNIQUE ("EmployeeNumber");
  END IF;
END $$;

-- Indexes for fast branch filtering
CREATE INDEX IF NOT EXISTS idx_employees_branch_id ON employees("BranchId");
CREATE INDEX IF NOT EXISTS idx_employees_employee_number ON employees("EmployeeNumber");
CREATE INDEX IF NOT EXISTS idx_employees_status ON employees("Status");
