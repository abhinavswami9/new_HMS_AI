-- Careflow HIS — Phase 1–3 depth migration: patient demographics with ABHA/Aadhaar
-- identity linkage (recorded boundary — no external ABDM gateway is integrated),
-- richer IPD admission registration and emergency arrival details. Additive only.
-- Apply with sqlcmd -b; idempotent per column.

-- ============================ patient demographics ========================
IF COL_LENGTH('patient.Patients', 'Title') IS NULL ALTER TABLE patient.Patients ADD Title VARCHAR(10) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'MiddleName') IS NULL ALTER TABLE patient.Patients ADD MiddleName NVARCHAR(80) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'MaritalStatus') IS NULL ALTER TABLE patient.Patients ADD MaritalStatus VARCHAR(20) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'FatherName') IS NULL ALTER TABLE patient.Patients ADD FatherName NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'SpouseName') IS NULL ALTER TABLE patient.Patients ADD SpouseName NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'MotherName') IS NULL ALTER TABLE patient.Patients ADD MotherName NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'GuardianName') IS NULL ALTER TABLE patient.Patients ADD GuardianName NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'GuardianRelation') IS NULL ALTER TABLE patient.Patients ADD GuardianRelation NVARCHAR(40) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Religion') IS NULL ALTER TABLE patient.Patients ADD Religion NVARCHAR(60) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Occupation') IS NULL ALTER TABLE patient.Patients ADD Occupation NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Education') IS NULL ALTER TABLE patient.Patients ADD Education NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Language') IS NULL ALTER TABLE patient.Patients ADD [Language] NVARCHAR(60) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Nationality') IS NULL ALTER TABLE patient.Patients ADD Nationality NVARCHAR(60) NOT NULL DEFAULT 'Indian';
GO
IF COL_LENGTH('patient.Patients', 'AadhaarNumber') IS NULL ALTER TABLE patient.Patients ADD AadhaarNumber VARCHAR(12) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'AbhaNumber') IS NULL ALTER TABLE patient.Patients ADD AbhaNumber VARCHAR(14) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'AbhaAddress') IS NULL ALTER TABLE patient.Patients ADD AbhaAddress NVARCHAR(100) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'City') IS NULL ALTER TABLE patient.Patients ADD City NVARCHAR(80) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'State') IS NULL ALTER TABLE patient.Patients ADD [State] NVARCHAR(80) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Pincode') IS NULL ALTER TABLE patient.Patients ADD Pincode VARCHAR(6) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('patient.Patients', 'Country') IS NULL ALTER TABLE patient.Patients ADD Country NVARCHAR(60) NOT NULL DEFAULT 'India';
GO


-- Identity linkage is unique when recorded: one patient per ABHA number/Aadhaar.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Patients_AbhaNumber')
    CREATE UNIQUE INDEX UX_Patients_AbhaNumber ON patient.Patients (HospitalId, AbhaNumber) WHERE AbhaNumber <> '';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Patients_AadhaarNumber')
    CREATE UNIQUE INDEX UX_Patients_AadhaarNumber ON patient.Patients (HospitalId, AadhaarNumber) WHERE AadhaarNumber <> '';
GO

-- ============================ ipd admission registration ==================
IF COL_LENGTH('ipd.Admissions', 'AdmissionType') IS NULL ALTER TABLE ipd.Admissions ADD AdmissionType VARCHAR(20) NOT NULL DEFAULT 'Elective';
GO
IF COL_LENGTH('ipd.Admissions', 'DiagnosisOnAdmission') IS NULL ALTER TABLE ipd.Admissions ADD DiagnosisOnAdmission NVARCHAR(500) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('ipd.Admissions', 'AttendantName') IS NULL ALTER TABLE ipd.Admissions ADD AttendantName NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('ipd.Admissions', 'AttendantPhone') IS NULL ALTER TABLE ipd.Admissions ADD AttendantPhone VARCHAR(30) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('ipd.Admissions', 'PayerType') IS NULL ALTER TABLE ipd.Admissions ADD PayerType VARCHAR(20) NOT NULL DEFAULT 'Self';
GO
IF COL_LENGTH('ipd.Admissions', 'CoverageId') IS NULL ALTER TABLE ipd.Admissions ADD CoverageId UNIQUEIDENTIFIER NULL;
GO
IF COL_LENGTH('ipd.Admissions', 'ExpectedDischargeDate') IS NULL ALTER TABLE ipd.Admissions ADD ExpectedDischargeDate DATE NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Admissions_AdmissionType')
    ALTER TABLE ipd.Admissions ADD CONSTRAINT CK_Admissions_AdmissionType CHECK (AdmissionType IN ('Emergency', 'Elective', 'Transfer', 'Obstetric'));
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Admissions_PayerType')
    ALTER TABLE ipd.Admissions ADD CONSTRAINT CK_Admissions_PayerType CHECK (PayerType IN ('Self', 'Insurance', 'Corporate'));
GO

-- ============================ emergency arrival details ===================
IF COL_LENGTH('emergency.Registrations', 'ReferringFacility') IS NULL ALTER TABLE emergency.Registrations ADD ReferringFacility NVARCHAR(200) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('emergency.Registrations', 'BroughtBy') IS NULL ALTER TABLE emergency.Registrations ADD BroughtBy NVARCHAR(120) NOT NULL DEFAULT '';
GO
IF COL_LENGTH('emergency.Registrations', 'MedicoLegalCase') IS NULL ALTER TABLE emergency.Registrations ADD MedicoLegalCase BIT NOT NULL DEFAULT 0;
GO
