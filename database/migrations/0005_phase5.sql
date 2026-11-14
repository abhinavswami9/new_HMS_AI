-- Careflow HIS — Phase 5 migration: quality/infection control & accreditation,
-- patient portal and management reporting. NABH/NABL-oriented evidence records,
-- not accreditation certification. Apply with sqlcmd -b; idempotent per batch.

IF SCHEMA_ID('quality') IS NULL EXEC('CREATE SCHEMA quality');
GO
IF SCHEMA_ID('portal') IS NULL EXEC('CREATE SCHEMA portal');
GO
IF SCHEMA_ID('report') IS NULL EXEC('CREATE SCHEMA report');
GO

-- ============================ quality =====================================
CREATE TABLE quality.Infections (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    PatientId UNIQUEIDENTIFIER NOT NULL,
    EncounterId UNIQUEIDENTIFIER NULL,
    Category VARCHAR(60) NOT NULL,
    Site NVARCHAR(120) NOT NULL,
    DetectedMethod NVARCHAR(120) NOT NULL,
    DetectedOn DATE NOT NULL,
    Severity VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    ReportedBy NVARCHAR(120) NOT NULL,
    ReportedAtUtc DATETIME2 NOT NULL,
    Investigation NVARCHAR(2000) NOT NULL DEFAULT '',
    Outcome NVARCHAR(1000) NOT NULL DEFAULT '',
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_Infections_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_Infections_Severity CHECK (Severity IN ('Mild', 'Moderate', 'Severe', 'Life-threatening')),
    CONSTRAINT CK_Infections_Status CHECK (Status IN ('Reported', 'Confirmed', 'Rejected', 'Closed'))
);
GO
CREATE TABLE quality.Incidents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    Category VARCHAR(60) NOT NULL,
    Severity VARCHAR(30) NOT NULL,
    Description NVARCHAR(4000) NOT NULL,
    Location NVARCHAR(200) NOT NULL,
    PatientId UNIQUEIDENTIFIER NULL,
    ReportedBy NVARCHAR(120) NOT NULL,
    ReportedAtUtc DATETIME2 NOT NULL,
    Status VARCHAR(30) NOT NULL,
    TriageNote NVARCHAR(2000) NOT NULL DEFAULT '',
    AssignedTo NVARCHAR(120) NOT NULL DEFAULT '',
    TriagedAtUtc DATETIME2 NULL,
    Investigation NVARCHAR(4000) NOT NULL DEFAULT '',
    RootCause NVARCHAR(2000) NOT NULL DEFAULT '',
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_Incidents_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_Incidents_Status CHECK (Status IN ('Reported', 'UnderInvestigation', 'CapaAssigned', 'Closed'))
);
GO
CREATE TABLE quality.RiskAssessments (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    Domain VARCHAR(60) NOT NULL,
    Description NVARCHAR(2000) NOT NULL,
    Probability INT NOT NULL,
    Impact INT NOT NULL,
    Mitigation NVARCHAR(2000) NOT NULL,
    Owner NVARCHAR(120) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    AssessedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_Risks_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_Risks_Score CHECK (Probability BETWEEN 1 AND 5 AND Impact BETWEEN 1 AND 5),
    CONSTRAINT CK_Risks_Status CHECK (Status IN ('Open', 'Mitigating', 'Closed'))
);
GO
CREATE TABLE quality.CAPA (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    IncidentId UNIQUEIDENTIFIER NULL,
    InfectionId UNIQUEIDENTIFIER NULL,
    AuditId UNIQUEIDENTIFIER NULL,
    RiskId UNIQUEIDENTIFIER NULL,
    Correction NVARCHAR(2000) NOT NULL,
    CorrectiveAction NVARCHAR(2000) NOT NULL,
    PreventiveAction NVARCHAR(2000) NOT NULL,
    Owner NVARCHAR(120) NOT NULL,
    DueAtUtc DATETIME2 NOT NULL,
    Status VARCHAR(30) NOT NULL,
    EffectivenessEvidence NVARCHAR(4000) NOT NULL DEFAULT '',
    EffectivenessVerified BIT NOT NULL DEFAULT 0,
    VerifiedBy NVARCHAR(120) NOT NULL DEFAULT '',
    VerifiedAtUtc DATETIME2 NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    CreatedBy NVARCHAR(120) NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_CAPA_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_CAPA_Sources CHECK (IncidentId IS NOT NULL OR InfectionId IS NOT NULL OR AuditId IS NOT NULL OR RiskId IS NOT NULL),
    CONSTRAINT CK_CAPA_Status CHECK (Status IN ('Open', 'InProgress', 'Verified', 'Closed', 'Cancelled'))
);
GO
CREATE TABLE quality.ClinicalAudits (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    Domain VARCHAR(200) NOT NULL,
    Scope NVARCHAR(2000) NOT NULL,
    PlannedFor DATE NOT NULL,
    Lead NVARCHAR(120) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    Findings NVARCHAR(8000) NOT NULL DEFAULT '',
    ScorePercent INT NOT NULL DEFAULT 0,
    Report NVARCHAR(8000) NOT NULL DEFAULT '',
    CreatedAtUtc DATETIME2 NOT NULL,
    CreatedBy NVARCHAR(120) NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_ClinicalAudits_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_ClinicalAudits_Status CHECK (Status IN ('Planned', 'InProgress', 'Closed')),
    CONSTRAINT CK_ClinicalAudits_Score CHECK (ScorePercent BETWEEN 0 AND 100)
);
GO
CREATE TABLE quality.Indicators (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Code VARCHAR(20) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    [Definition] NVARCHAR(2000) NOT NULL,
    Numerator NVARCHAR(500) NOT NULL,
    Denominator NVARCHAR(500) NOT NULL,
    [Period] VARCHAR(40) NOT NULL,
    Owner NVARCHAR(120) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_Indicators_Code UNIQUE (HospitalId, Code),
    CONSTRAINT CK_Indicators_Status CHECK (Status IN ('Active', 'Retired'))
);
GO
CREATE TABLE quality.IndicatorResults (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    IndicatorId UNIQUEIDENTIFIER NOT NULL,
    PeriodStart VARCHAR(7) NOT NULL,
    PeriodEnd VARCHAR(7) NOT NULL,
    Numerator DECIMAL(18, 4) NOT NULL,
    Denominator DECIMAL(18, 4) NOT NULL,
    Value DECIMAL(18, 4) NOT NULL,
    Basis NVARCHAR(500) NOT NULL,
    Note NVARCHAR(1000) NOT NULL DEFAULT '',
    RecordedBy NVARCHAR(120) NOT NULL,
    RecordedAtUtc DATETIME2 NOT NULL,
    CONSTRAINT CK_IndicatorResults_Period CHECK (PeriodStart <= PeriodEnd),
    CONSTRAINT CK_IndicatorResults_Ratio CHECK (Denominator > 0 AND Numerator >= 0 AND Numerator <= Denominator)
);
GO
CREATE TABLE quality.Documents (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    Title NVARCHAR(300) NOT NULL,
    Category VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    CurrentVersion INT NOT NULL,
    Owner NVARCHAR(120) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_Documents_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_Documents_Category CHECK (Category IN ('SOP', 'Policy', 'Guideline', 'Form', 'Plan')),
    CONSTRAINT CK_Documents_Status CHECK (Status IN ('Draft', 'InReview', 'Approved', 'Published', 'Retired'))
);
GO
CREATE TABLE quality.DocumentVersions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    Number INT NOT NULL,
    Summary NVARCHAR(1000) NOT NULL,
    Content NVARCHAR(20000) NOT NULL,
    CreatedBy NVARCHAR(120) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    ApprovedBy NVARCHAR(120) NOT NULL DEFAULT '',
    ApprovedAtUtc DATETIME2 NULL,
    CONSTRAINT UX_DocumentVersions_Number UNIQUE (DocumentId, Number)
);
GO
CREATE TABLE quality.SOPAcknowledgments (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    DocumentId UNIQUEIDENTIFIER NOT NULL,
    VersionNumber INT NOT NULL,
    UserName NVARCHAR(120) NOT NULL,
    Attestation NVARCHAR(500) NOT NULL,
    AcknowledgedAtUtc DATETIME2 NOT NULL,
    CONSTRAINT UX_SOPAcknowledgments UNIQUE (DocumentId, VersionNumber, UserName)
);
GO

-- ============================ portal ======================================
CREATE TABLE portal.Accounts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    PatientId UNIQUEIDENTIFIER NOT NULL,
    UserName NVARCHAR(60) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    ProofingEvidence NVARCHAR(500) NOT NULL,
    VerifiedBy NVARCHAR(120) NOT NULL DEFAULT '',
    VerifiedAtUtc DATETIME2 NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_PortalAccounts_User UNIQUE (HospitalId, UserId),
    CONSTRAINT UX_PortalAccounts_Patient UNIQUE (HospitalId, PatientId),
    CONSTRAINT UX_PortalAccounts_UserName UNIQUE (HospitalId, UserName),
    CONSTRAINT CK_PortalAccounts_Status CHECK (Status IN ('Pending', 'Verified', 'Suspended'))
);
GO
CREATE TABLE portal.PatientLinks (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    PatientId UNIQUEIDENTIFIER NOT NULL,
    Relationship VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    ProofingEvidence NVARCHAR(500) NOT NULL,
    LinkedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_PatientLinks UNIQUE (AccountId, PatientId),
    CONSTRAINT CK_PatientLinks_Relationship CHECK (Relationship IN ('Self', 'Parent', 'Spouse', 'Guardian', 'Proxy')),
    CONSTRAINT CK_PatientLinks_Status CHECK (Status IN ('Active', 'Revoked'))
);
GO
CREATE TABLE portal.ProxyGrants (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    PatientId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Scope VARCHAR(30) NOT NULL,
    ExpiresOn DATE NOT NULL,
    Status VARCHAR(30) NOT NULL,
    GrantedBy NVARCHAR(120) NOT NULL,
    GrantedAtUtc DATETIME2 NOT NULL,
    RevokedBy NVARCHAR(120) NOT NULL DEFAULT '',
    RevokedAtUtc DATETIME2 NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT CK_ProxyGrants_Scope CHECK (Scope IN ('Appointments', 'Records', 'Billing')),
    CONSTRAINT CK_ProxyGrants_Status CHECK (Status IN ('Active', 'Revoked', 'Expired'))
);
GO
CREATE TABLE portal.DeviceSessions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Device NVARCHAR(120) NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    IssuedAtUtc DATETIME2 NOT NULL,
    ExpiresAtUtc DATETIME2 NOT NULL,
    Status VARCHAR(30) NOT NULL,
    CONSTRAINT CK_DeviceSessions_Status CHECK (Status IN ('Active', 'Revoked', 'Expired'))
);
GO
CREATE TABLE portal.Preferences (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    ReminderChannel VARCHAR(30) NOT NULL,
    Language NVARCHAR(40) NOT NULL,
    PaperlessBilling BIT NOT NULL DEFAULT 1,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_PortalPreferences_Account UNIQUE (AccountId)
);
GO

-- ============================ reporting ===================================
CREATE TABLE report.Definitions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Code VARCHAR(20) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Category VARCHAR(30) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    CurrentVersion INT NOT NULL,
    Owner NVARCHAR(120) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_ReportDefinitions_Code UNIQUE (HospitalId, Code),
    CONSTRAINT CK_ReportDefinitions_Category CHECK (Category IN ('Clinical', 'Financial', 'Management', 'Quality')),
    CONSTRAINT CK_ReportDefinitions_Status CHECK (Status IN ('Draft', 'Active', 'Retired'))
);
GO
CREATE TABLE report.Versions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    DefinitionId UNIQUEIDENTIFIER NOT NULL,
    Number INT NOT NULL,
    QueryKey VARCHAR(60) NOT NULL,
    Columns NVARCHAR(500) NOT NULL,
    Filters NVARCHAR(500) NOT NULL DEFAULT '',
    CreatedBy NVARCHAR(120) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    ApprovedBy NVARCHAR(120) NOT NULL DEFAULT '',
    ApprovedAtUtc DATETIME2 NULL,
    CONSTRAINT UX_ReportVersions UNIQUE (DefinitionId, Number)
);
GO
CREATE TABLE report.Executions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    Number VARCHAR(40) NOT NULL,
    DefinitionId UNIQUEIDENTIFIER NOT NULL,
    DefinitionVersion INT NOT NULL,
    Parameters NVARCHAR(1000) NOT NULL,
    [Rows] NVARCHAR(MAX) NOT NULL,
    [RowCount] INT NOT NULL,
    Status VARCHAR(30) NOT NULL,
    ExecutedBy NVARCHAR(120) NOT NULL,
    ExecutedAtUtc DATETIME2 NOT NULL,
    Error NVARCHAR(1000) NOT NULL DEFAULT '',
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT UX_ReportExecutions_Number UNIQUE (HospitalId, Number),
    CONSTRAINT CK_ReportExecutions_Status CHECK (Status IN ('Completed', 'Failed'))
);
GO
CREATE TABLE report.Exports (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    ExecutionId UNIQUEIDENTIFIER NOT NULL,
    Format VARCHAR(10) NOT NULL,
    Status VARCHAR(30) NOT NULL,
    Watermark NVARCHAR(500) NOT NULL,
    FileRef NVARCHAR(200) NOT NULL,
    RequestedBy NVARCHAR(120) NOT NULL,
    RequestedAtUtc DATETIME2 NOT NULL,
    CONSTRAINT CK_ReportExports_Format CHECK (Format IN ('PDF', 'Excel'))
);
GO
CREATE TABLE report.Subscriptions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    HospitalId UNIQUEIDENTIFIER NOT NULL,
    BranchId UNIQUEIDENTIFIER NOT NULL,
    DefinitionId UNIQUEIDENTIFIER NOT NULL,
    Cadence VARCHAR(20) NOT NULL,
    Recipients NVARCHAR(500) NOT NULL,
    Active BIT NOT NULL DEFAULT 1,
    CreatedBy NVARCHAR(120) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    Version BIGINT NOT NULL DEFAULT 1,
    CONSTRAINT CK_ReportSubscriptions_Cadence CHECK (Cadence IN ('Daily', 'Weekly', 'Monthly'))
);
GO

-- ============================ immutability triggers ========================
CREATE TRIGGER quality.ProtectClosedInfections
ON quality.Infections
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status) AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id WHERE d.Status = 'Closed' AND i.Status <> 'Closed')
    BEGIN
        RAISERROR('Closed infection surveillance records are immutable.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.ProtectClosedIncidents
ON quality.Incidents
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status) AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id WHERE d.Status = 'Closed' AND i.Status <> 'Closed')
    BEGIN
        RAISERROR('Closed incidents are immutable.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.ProtectClosedCAPA
ON quality.CAPA
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status) AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id WHERE d.Status = 'Closed' AND i.Status <> 'Closed')
    BEGIN
        RAISERROR('Closed CAPA records are immutable.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.ProtectVerifiedCAPA
ON quality.CAPA
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(EffectivenessVerified) AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id WHERE d.EffectivenessVerified = 1 AND i.EffectivenessVerified = 0)
    BEGIN
        RAISERROR('Verified effectiveness evidence cannot be withdrawn; open a new CAPA instead.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.AppendOnlyDocumentVersions
ON quality.DocumentVersions
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted WHERE ApprovedAtUtc IS NOT NULL)
    BEGIN
        RAISERROR('Document versions are append-only; publish a new version instead.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.AppendOnlySOPAcknowledgments
ON quality.SOPAcknowledgments
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        RAISERROR('SOP acknowledgments are append-only.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER quality.AppendOnlyIndicatorResults
ON quality.IndicatorResults
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        RAISERROR('Indicator results are append-only; record a corrected period instead.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER portal.AppendOnlyDeviceSessions
ON portal.DeviceSessions
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        RAISERROR('Portal device sessions are append-only.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER portal.ProtectRevokedProxyGrants
ON portal.ProxyGrants
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status) AND EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id WHERE d.Status = 'Revoked' AND i.Status <> 'Revoked')
    BEGIN
        RAISERROR('Revoked proxy grants are immutable; issue a new grant instead.', 16, 1);
        ROLLBACK;
    END
END;
GO
CREATE TRIGGER report.AppendOnlyExecutions
ON report.Executions
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        RAISERROR('Report executions are append-only audit records.', 16, 1);
        ROLLBACK;
    END
END;
GO

-- ============================ indexes =====================================
CREATE INDEX IX_Infections_Patient ON quality.Infections (HospitalId, PatientId);
CREATE INDEX IX_Incidents_Status ON quality.Incidents (HospitalId, Status);
CREATE INDEX IX_CAPA_Due ON quality.CAPA (HospitalId, Status, DueAtUtc);
CREATE INDEX IX_IndicatorResults_Period ON quality.IndicatorResults (HospitalId, IndicatorId, PeriodStart);
CREATE INDEX IX_PortalAccounts_User ON portal.Accounts (HospitalId, UserId);
CREATE INDEX IX_ProxyGrants_Patient ON portal.ProxyGrants (HospitalId, PatientId, Status);
CREATE INDEX IX_ReportExecutions_Definition ON report.Executions (HospitalId, DefinitionId, ExecutedAtUtc);
GO
