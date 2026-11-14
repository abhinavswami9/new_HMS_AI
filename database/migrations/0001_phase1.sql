-- Careflow Phase 1 foundation. SQL Server 2022+.
-- Run through Careflow.Api migrate, which records checksums and executes in a transaction.
-- No synthetic patients or usable credentials are seeded by this migration.
SET XACT_ABORT ON;
GO
CREATE TABLE org.Hospitals (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), Code varchar(30) NOT NULL UNIQUE,
 Name nvarchar(160) NOT NULL, TimeZoneId varchar(80) NOT NULL DEFAULT 'Asia/Kolkata', Currency char(3) NOT NULL DEFAULT 'INR',
 CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), Active bit NOT NULL DEFAULT 1
);
CREATE TABLE org.Branches (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 Code varchar(30) NOT NULL, Name nvarchar(160) NOT NULL, TimeZoneId varchar(80) NOT NULL DEFAULT 'Asia/Kolkata',
 Active bit NOT NULL DEFAULT 1, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Code), UNIQUE(HospitalId,Id)
);
CREATE TABLE org.Departments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Name nvarchar(120) NOT NULL, Active bit NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id), UNIQUE(HospitalId,BranchId,Name),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE org.ModuleCatalog (
 [Key] varchar(40) PRIMARY KEY, Name nvarchar(160) NOT NULL, Phase int NOT NULL CHECK(Phase BETWEEN 1 AND 5), Description nvarchar(600) NOT NULL
);
CREATE TABLE org.ModuleInstallations (
 HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, ModuleKey varchar(40) NOT NULL REFERENCES org.ModuleCatalog([Key]),
 Enabled bit NOT NULL DEFAULT 0, UpdatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedBy uniqueidentifier NULL,
 PRIMARY KEY(HospitalId,BranchId,ModuleKey), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE org.Policies (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 [Key] varchar(80) NOT NULL, Version int NOT NULL CHECK(Version>0), PayloadJson nvarchar(max) NOT NULL CHECK(ISJSON(PayloadJson)=1),
 EffectiveFromUtc datetime2(3) NOT NULL, ApprovedBy uniqueidentifier NULL, Reason nvarchar(500) NOT NULL,
 UNIQUE(HospitalId,BranchId,[Key],Version), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE org.NumberSequences (
 HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id), Kind varchar(20) NOT NULL, ScopeKey varchar(120) NOT NULL,
 Prefix varchar(40) NOT NULL, CurrentValue bigint NOT NULL DEFAULT 0 CHECK(CurrentValue>=0), PRIMARY KEY(HospitalId,Kind,ScopeKey)
);
GO
CREATE TABLE iam.Users (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 UserName varchar(100) NOT NULL UNIQUE, DisplayName nvarchar(160) NOT NULL, PasswordHash varchar(500) NOT NULL,
 Enabled bit NOT NULL DEFAULT 1, FailedAttempts int NOT NULL DEFAULT 0, LockedUntilUtc datetime2(3) NULL,
 MfaEnabled bit NOT NULL DEFAULT 0, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(3) NULL,
 RowVer rowversion, UNIQUE(HospitalId,Id)
);
CREATE TABLE iam.Roles (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 Name nvarchar(80) NOT NULL, Description nvarchar(400) NULL, UNIQUE(HospitalId,Name), UNIQUE(HospitalId,Id)
);
CREATE TABLE iam.Permissions ([Key] varchar(80) PRIMARY KEY, Description nvarchar(300) NOT NULL);
CREATE TABLE iam.RolePermissions (
 HospitalId uniqueidentifier NOT NULL, RoleId uniqueidentifier NOT NULL, PermissionKey varchar(80) NOT NULL REFERENCES iam.Permissions([Key]),
 PRIMARY KEY(HospitalId,RoleId,PermissionKey), FOREIGN KEY(HospitalId,RoleId) REFERENCES iam.Roles(HospitalId,Id)
);
CREATE TABLE iam.Memberships (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 UserId uniqueidentifier NOT NULL, Active bit NOT NULL DEFAULT 1, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,BranchId,UserId), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id), FOREIGN KEY(HospitalId,UserId) REFERENCES iam.Users(HospitalId,Id)
);
CREATE TABLE iam.UserRoles (
 MembershipId uniqueidentifier NOT NULL REFERENCES iam.Memberships(Id), RoleId uniqueidentifier NOT NULL, HospitalId uniqueidentifier NOT NULL,
 PRIMARY KEY(MembershipId,RoleId), FOREIGN KEY(HospitalId,RoleId) REFERENCES iam.Roles(HospitalId,Id)
);
CREATE TABLE iam.Sessions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, UserId uniqueidentifier NOT NULL,
 TokenHash char(64) NOT NULL UNIQUE, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ExpiresAtUtc datetime2(3) NOT NULL,
 RevokedAtUtc datetime2(3) NULL, FOREIGN KEY(HospitalId,UserId) REFERENCES iam.Users(HospitalId,Id), CHECK(ExpiresAtUtc>CreatedAtUtc)
);
CREATE INDEX IX_Sessions_UserId ON iam.Sessions(UserId,ExpiresAtUtc);
CREATE TABLE iam.LoginEvents (
 Id bigint IDENTITY PRIMARY KEY, HospitalId uniqueidentifier NOT NULL, UserId uniqueidentifier NULL, UserName varchar(100) NOT NULL,
 Success bit NOT NULL, SourceIp varchar(45) NULL, OccurredAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_LoginEvents_Time ON iam.LoginEvents(HospitalId,OccurredAtUtc DESC);
GO
CREATE TABLE catalog.Practitioners (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 UserId uniqueidentifier NULL, Name nvarchar(160) NOT NULL, Department nvarchar(120) NOT NULL, Initials varchar(5) NOT NULL, Color varchar(10) NOT NULL,
 Active bit NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,UserId) REFERENCES iam.Users(HospitalId,Id)
);
CREATE TABLE catalog.Services (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 Code varchar(40) NOT NULL, Name nvarchar(180) NOT NULL, Department nvarchar(120) NOT NULL, Active bit NOT NULL DEFAULT 1,
 Version int NOT NULL DEFAULT 1, UNIQUE(HospitalId,Code), UNIQUE(HospitalId,Id)
);
CREATE TABLE catalog.Prices (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 ServiceId uniqueidentifier NOT NULL, Amount decimal(19,4) NOT NULL CHECK(Amount>=0), Currency char(3) NOT NULL DEFAULT 'INR',
 EffectiveFromUtc datetime2(3) NOT NULL, EffectiveToUtc datetime2(3) NULL, CreatedBy uniqueidentifier NULL,
 UNIQUE(HospitalId,BranchId,ServiceId,EffectiveFromUtc), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id),
 FOREIGN KEY(HospitalId,ServiceId) REFERENCES catalog.Services(HospitalId,Id), CHECK(EffectiveToUtc IS NULL OR EffectiveToUtc>EffectiveFromUtc)
);
CREATE TABLE catalog.Medicines (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 GenericName nvarchar(200) NOT NULL, BrandName nvarchar(200) NOT NULL, Strength nvarchar(80) NOT NULL, Form nvarchar(60) NOT NULL,
 CodeSystem varchar(80) NULL, Code varchar(80) NULL, Active bit NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id)
);
CREATE TABLE catalog.PrescriptionTemplates (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 OwnerId uniqueidentifier NULL, Name nvarchar(160) NOT NULL, ContentJson nvarchar(max) NOT NULL CHECK(ISJSON(ContentJson)=1), Version int NOT NULL,
 Active bit NOT NULL DEFAULT 1, FOREIGN KEY(HospitalId,OwnerId) REFERENCES iam.Users(HospitalId,Id)
);
GO
CREATE TABLE patient.Patients (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL REFERENCES org.Hospitals(Id),
 BranchId uniqueidentifier NOT NULL, Uhid varchar(40) NOT NULL, FirstName nvarchar(80) NOT NULL, LastName nvarchar(80) NOT NULL,
 DateOfBirth date NOT NULL, Sex varchar(20) NOT NULL CHECK(Sex IN ('Male','Female','Other','Undisclosed')),
 Phone varchar(20) NOT NULL DEFAULT '', Email nvarchar(254) NOT NULL DEFAULT '', BloodGroup varchar(10) NOT NULL DEFAULT 'Unknown',
 Address nvarchar(500) NOT NULL DEFAULT '', EmergencyContact nvarchar(200) NOT NULL DEFAULT '', Active bit NOT NULL DEFAULT 1,
 Version bigint NOT NULL DEFAULT 1, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy uniqueidentifier NOT NULL,
 UpdatedAtUtc datetime2(3) NULL, UpdatedBy uniqueidentifier NULL, RowVer rowversion,
 UNIQUE(HospitalId,Uhid), UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE INDEX IX_Patients_Phone ON patient.Patients(HospitalId,Phone) INCLUDE(FirstName,LastName,DateOfBirth);
CREATE INDEX IX_Patients_Name ON patient.Patients(HospitalId,LastName,FirstName,DateOfBirth);
CREATE TABLE patient.Contacts (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 Name nvarchar(160) NOT NULL, Relationship nvarchar(60) NOT NULL, Phone varchar(20) NOT NULL, IsEmergency bit NOT NULL DEFAULT 0,
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
CREATE TABLE patient.Consents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 Purpose varchar(80) NOT NULL, PolicyVersion int NOT NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Granted','Withdrawn','Declined')),
 CapturedAtUtc datetime2(3) NOT NULL, CapturedBy uniqueidentifier NOT NULL, EvidenceDocumentId uniqueidentifier NULL, Reason nvarchar(500) NULL,
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
CREATE TABLE patient.IdentityDocuments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 DocumentType varchar(60) NOT NULL, ObjectKey varchar(500) NOT NULL, Sha256 char(64) NOT NULL, VerifiedAtUtc datetime2(3) NULL,
 VerifiedBy uniqueidentifier NULL, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
CREATE TABLE patient.MergeCases (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, SourcePatientId uniqueidentifier NOT NULL,
 SurvivorPatientId uniqueidentifier NOT NULL, Reason nvarchar(1000) NOT NULL, EvidenceJson nvarchar(max) NOT NULL CHECK(ISJSON(EvidenceJson)=1),
 Status varchar(20) NOT NULL CHECK(Status IN ('Requested','Approved','Rejected','Completed')), RequestedBy uniqueidentifier NOT NULL,
 ApprovedBy uniqueidentifier NULL, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), CompletedAtUtc datetime2(3) NULL,
 FOREIGN KEY(HospitalId,SourcePatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,SurvivorPatientId) REFERENCES patient.Patients(HospitalId,Id),
 CHECK(SourcePatientId<>SurvivorPatientId), CHECK(ApprovedBy IS NULL OR ApprovedBy<>RequestedBy)
);
CREATE TABLE patient.MergeMappings (
 MergeCaseId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES patient.MergeCases(Id), HospitalId uniqueidentifier NOT NULL,
 SourceUhid varchar(40) NOT NULL, SurvivorUhid varchar(40) NOT NULL, SnapshotJson nvarchar(max) NOT NULL CHECK(ISJSON(SnapshotJson)=1), CompletedAtUtc datetime2(3) NOT NULL
);
CREATE TABLE patient.Links (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 System varchar(40) NOT NULL, IdentifierHash char(64) NOT NULL, VerificationEvidence nvarchar(500) NULL, VerifiedAtUtc datetime2(3) NULL,
 UNIQUE(HospitalId,System,IdentifierHash), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
GO
CREATE TABLE sched.Schedules (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 DoctorId uniqueidentifier NOT NULL, Weekday tinyint NOT NULL CHECK(Weekday BETWEEN 0 AND 6), StartTime time(0) NOT NULL, EndTime time(0) NOT NULL,
 SlotMinutes int NOT NULL CHECK(SlotMinutes BETWEEN 5 AND 120), EffectiveFrom date NOT NULL, EffectiveTo date NULL,
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id), FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id), CHECK(EndTime>StartTime)
);
CREATE TABLE sched.ScheduleExceptions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), ScheduleId uniqueidentifier NOT NULL REFERENCES sched.Schedules(Id),
 HospitalId uniqueidentifier NOT NULL, ExceptionDate date NOT NULL, Reason nvarchar(500) NOT NULL, UNIQUE(ScheduleId,ExceptionDate)
);
CREATE TABLE sched.Appointments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 PatientId uniqueidentifier NOT NULL, DoctorId uniqueidentifier NOT NULL, StartsAtUtc datetime2(3) NOT NULL, DurationMinutes int NOT NULL CHECK(DurationMinutes BETWEEN 5 AND 120),
 Mode varchar(20) NOT NULL, Reason nvarchar(500) NOT NULL DEFAULT '', Status varchar(25) NOT NULL CHECK(Status IN ('Booked','Checked in','In consultation','Completed','Cancelled')),
 Token int NULL CHECK(Token>0), ServiceDateUtc AS CONVERT(date,StartsAtUtc) PERSISTED, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 CreatedBy uniqueidentifier NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE UNIQUE INDEX UX_Appointments_Slot ON sched.Appointments(BranchId,DoctorId,StartsAtUtc) WHERE Status<>'Cancelled';
CREATE UNIQUE INDEX UX_Appointments_Token ON sched.Appointments(BranchId,DoctorId,ServiceDateUtc,Token) WHERE Token IS NOT NULL;
CREATE INDEX IX_Appointments_Queue ON sched.Appointments(HospitalId,BranchId,StartsAtUtc DESC,Status) INCLUDE(PatientId,DoctorId,Token);
CREATE TABLE sched.Slots (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 DoctorId uniqueidentifier NOT NULL, StartsAtUtc datetime2(3) NOT NULL, EndsAtUtc datetime2(3) NOT NULL, AppointmentId uniqueidentifier NULL,
 UNIQUE(BranchId,DoctorId,StartsAtUtc), FOREIGN KEY(HospitalId,AppointmentId) REFERENCES sched.Appointments(HospitalId,Id), CHECK(EndsAtUtc>StartsAtUtc)
);
CREATE TABLE sched.QueueEntries (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, AppointmentId uniqueidentifier NOT NULL UNIQUE,
 Token int NOT NULL, Status varchar(25) NOT NULL, EnteredAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), CalledAtUtc datetime2(3) NULL,
 FOREIGN KEY(HospitalId,AppointmentId) REFERENCES sched.Appointments(HospitalId,Id)
);
GO
CREATE TABLE emr.Encounters (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 PatientId uniqueidentifier NOT NULL, AppointmentId uniqueidentifier NULL, DoctorId uniqueidentifier NOT NULL,
 Status varchar(10) NOT NULL CHECK(Status IN ('Draft','Signed')), ChiefComplaint nvarchar(max) NOT NULL DEFAULT '', Examination nvarchar(max) NOT NULL DEFAULT '',
 Diagnosis nvarchar(max) NOT NULL DEFAULT '', [Plan] nvarchar(max) NOT NULL DEFAULT '', FollowUp nvarchar(max) NOT NULL DEFAULT '',
 TemperatureC decimal(5,2) NULL, Pulse int NULL, Systolic int NULL, Diastolic int NULL, WeightKg decimal(6,2) NULL, Spo2 int NULL,
 AllergyReviewed bit NOT NULL DEFAULT 0, SignedBy nvarchar(160) NULL, SignedAtUtc datetime2(3) NULL, ContentHash char(64) NULL,
 UpdatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id),
 FOREIGN KEY(HospitalId,AppointmentId) REFERENCES sched.Appointments(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id),
 CHECK((Status='Draft' AND SignedAtUtc IS NULL AND ContentHash IS NULL) OR (Status='Signed' AND SignedBy IS NOT NULL AND SignedAtUtc IS NOT NULL AND ContentHash IS NOT NULL AND AllergyReviewed=1))
);
CREATE UNIQUE INDEX UX_Encounters_Appointment ON emr.Encounters(AppointmentId) WHERE AppointmentId IS NOT NULL;
CREATE INDEX IX_Encounters_Patient ON emr.Encounters(HospitalId,PatientId,UpdatedAtUtc DESC);
CREATE INDEX IX_Encounters_Worklist ON emr.Encounters(HospitalId,BranchId,DoctorId,Status,UpdatedAtUtc DESC);
CREATE TABLE emr.Allergies (
 EncounterId uniqueidentifier NOT NULL REFERENCES emr.Encounters(Id), HospitalId uniqueidentifier NOT NULL, Ordinal int NOT NULL,
 Substance nvarchar(200) NOT NULL, Reaction nvarchar(500) NOT NULL, Severity nvarchar(40) NOT NULL, PRIMARY KEY(EncounterId,Ordinal),
 FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id)
);
-- The initial single-encounter allergy snapshot is intentionally explicit. A longitudinal,
-- independently versioned allergy registry is a subsequent Phase 1 increment.
CREATE TABLE emr.PrescriptionItems (
 EncounterId uniqueidentifier NOT NULL, HospitalId uniqueidentifier NOT NULL, Ordinal int NOT NULL, MedicineId uniqueidentifier NOT NULL,
 Name nvarchar(280) NOT NULL, Dose nvarchar(80) NOT NULL, Frequency nvarchar(80) NOT NULL, DurationDays int NOT NULL CHECK(DurationDays BETWEEN 1 AND 365),
 Instructions nvarchar(500) NOT NULL, PRIMARY KEY(EncounterId,Ordinal), FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id),
 FOREIGN KEY(HospitalId,MedicineId) REFERENCES catalog.Medicines(HospitalId,Id)
);
CREATE TABLE emr.Orders (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NOT NULL,
 Ordinal int NOT NULL, Kind varchar(20) NOT NULL CHECK(Kind IN ('Laboratory','Radiology','Procedure')), Name nvarchar(200) NOT NULL,
 Instructions nvarchar(1000) NOT NULL, Status varchar(20) NOT NULL DEFAULT 'Recorded', UNIQUE(EncounterId,Ordinal),
 FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id)
);
CREATE TABLE emr.RecordVersions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NOT NULL,
 Version bigint NOT NULL, SnapshotJson nvarchar(max) NOT NULL CHECK(ISJSON(SnapshotJson)=1), ContentHash char(64) NULL,
 CreatedBy uniqueidentifier NOT NULL, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), UNIQUE(EncounterId,Version),
 FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id)
);
GO
CREATE TABLE bill.Invoices (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, PatientId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NULL,
 Status varchar(15) NOT NULL CHECK(Status IN ('Draft','Issued','Part paid','Paid')), Discount decimal(19,4) NOT NULL DEFAULT 0 CHECK(Discount>=0),
 TaxRate decimal(9,6) NOT NULL DEFAULT 0 CHECK(TaxRate BETWEEN 0 AND 1), CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 IssuedAtUtc datetime2(3) NULL, CreatedBy uniqueidentifier NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Number), UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE INDEX IX_Invoices_Worklist ON bill.Invoices(HospitalId,BranchId,Status,CreatedAtUtc DESC);
CREATE TABLE bill.Lines (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, InvoiceId uniqueidentifier NOT NULL,
 ServiceId uniqueidentifier NOT NULL, Name nvarchar(180) NOT NULL, Quantity int NOT NULL CHECK(Quantity BETWEEN 1 AND 100),
 UnitPrice decimal(19,4) NOT NULL CHECK(UnitPrice>=0), FOREIGN KEY(HospitalId,InvoiceId) REFERENCES bill.Invoices(HospitalId,Id),
 FOREIGN KEY(HospitalId,ServiceId) REFERENCES catalog.Services(HospitalId,Id)
);
CREATE TABLE bill.Payments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, InvoiceId uniqueidentifier NOT NULL,
 Amount decimal(19,4) NOT NULL CHECK(Amount>0), Method varchar(20) NOT NULL, Reference varchar(100) NOT NULL,
 ReceivedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ReceivedBy uniqueidentifier NOT NULL,
 UNIQUE(HospitalId,Method,Reference), FOREIGN KEY(HospitalId,InvoiceId) REFERENCES bill.Invoices(HospitalId,Id)
);
CREATE TABLE bill.ApprovalRequests (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, InvoiceId uniqueidentifier NOT NULL,
 Kind varchar(20) NOT NULL CHECK(Kind IN ('Discount','Refund')), Amount decimal(19,4) NOT NULL CHECK(Amount>0), Reason nvarchar(1000) NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Requested','Approved','Rejected')), RequestedBy uniqueidentifier NOT NULL, ApprovedBy uniqueidentifier NULL,
 CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), FOREIGN KEY(HospitalId,InvoiceId) REFERENCES bill.Invoices(HospitalId,Id), CHECK(ApprovedBy IS NULL OR ApprovedBy<>RequestedBy)
);
CREATE TABLE bill.CreditNotes (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, InvoiceId uniqueidentifier NOT NULL,
 Amount decimal(19,4) NOT NULL CHECK(Amount>0), Reason nvarchar(1000) NOT NULL, ApprovalRequestId uniqueidentifier NOT NULL REFERENCES bill.ApprovalRequests(Id),
 CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy uniqueidentifier NOT NULL, FOREIGN KEY(HospitalId,InvoiceId) REFERENCES bill.Invoices(HospitalId,Id)
);
GO
CREATE TABLE audit.AuditEvents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 ActorId uniqueidentifier NOT NULL, ActorName nvarchar(160) NOT NULL, Action varchar(100) NOT NULL, Entity varchar(60) NOT NULL,
 EntityId uniqueidentifier NOT NULL, Reason nvarchar(1000) NOT NULL DEFAULT '', ChangesJson nvarchar(max) NULL CHECK(ChangesJson IS NULL OR ISJSON(ChangesJson)=1),
 OccurredAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), CorrelationId varchar(80) NULL
);
CREATE INDEX IX_Audit_Entity ON audit.AuditEvents(HospitalId,Entity,EntityId,OccurredAtUtc DESC);
CREATE INDEX IX_Audit_Time ON audit.AuditEvents(HospitalId,OccurredAtUtc DESC);
CREATE TABLE integration.OutboxMessages (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Type varchar(100) NOT NULL, AggregateId uniqueidentifier NOT NULL, PayloadJson nvarchar(max) NOT NULL CHECK(ISJSON(PayloadJson)=1),
 Status varchar(20) NOT NULL DEFAULT 'Pending', Attempts int NOT NULL DEFAULT 0, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), NextAttemptAtUtc datetime2(3) NULL,
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE INDEX IX_Outbox_Pending ON integration.OutboxMessages(Status,NextAttemptAtUtc,CreatedAtUtc) WHERE Status='Pending';
CREATE TABLE integration.InboxMessages (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, Source varchar(80) NOT NULL,
 ExternalMessageId varchar(160) NOT NULL, PayloadHash char(64) NOT NULL, ReceivedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ProcessedAtUtc datetime2(3) NULL,
 UNIQUE(HospitalId,Source,ExternalMessageId)
);
CREATE TABLE storage.Documents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NULL,
 ObjectKey varchar(500) NOT NULL UNIQUE, Classification varchar(60) NOT NULL, ContentType varchar(100) NOT NULL, SizeBytes bigint NOT NULL CHECK(SizeBytes>=0),
 Sha256 char(64) NOT NULL, UploadedBy uniqueidentifier NOT NULL, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
GO
INSERT INTO iam.Permissions([Key],Description) VALUES
('patient.read','Read scoped patient demographics'),('patient.write','Register and update patient demographics'),
('schedule.read','Read scoped schedules and queues'),('schedule.write','Book and manage appointments'),
('clinical.read','Read authorized clinical records'),('clinical.write','Write assigned clinical drafts'),
('clinical.sign','Sign assigned clinical records; qualified clinicians only'),('billing.read','Read scoped invoices'),
('billing.write','Create and issue invoices'),('billing.collect','Record received payments'),
('security.admin','Manage scoped configuration and access'),('audit.read','Read scoped audit events');
INSERT INTO org.ModuleCatalog([Key],Name,Phase,Description) VALUES
('patients','Patient registration & UHID',1,'Patient identity, demographics and duplicate review.'),
('appointments','Appointments & queues',1,'Scheduling, check-in and patient flow.'),
('consultations','OPD & doctor EMR',1,'Clinical notes, prescriptions and signed records.'),
('billing','Billing & payments',1,'Service invoices and payment collection.'),
('ipd','Inpatient management',2,'ADT and discharge workflows.'),('nursing','Nursing & bed management',2,'Bed board, nursing and eMAR.'),
('pharmacy','Pharmacy management',2,'Dispensing and batch stock.'),('laboratory','Laboratory · LIS',2,'Specimens, results and authorization.'),
('radiology','Radiology · RIS',3,'Modality worklists and reporting.'),('pacs','PACS & DICOM',3,'Imaging integration.'),
('emergency','Emergency & casualty',3,'Triage and emergency care.'),('ot','Operation theatre',3,'Surgical scheduling and safety.'),
('blood','Blood bank',3,'Donor-to-recipient traceability.'),('insurance','Insurance & TPA',4,'Coverage and claims.'),
('incentives','Doctor incentives',4,'Configurable incentive calculation.'),('telemedicine','Telemedicine',4,'Video consultation integration.'),
('inventory','Inventory & procurement',4,'Stores and purchasing.'),('cssd','CSSD',4,'Sterilization traceability.'),
('quality','Quality & infection control',5,'Audits, incidents and CAPA.'),('portal','Patient portal',5,'Verified patient access.'),
('reports','Advanced reporting',5,'Governed reporting and exports.');
GO
-- Defense in depth: signed clinical content and its children cannot be overwritten.
CREATE TRIGGER emr.ProtectSignedEncounter ON emr.Encounters AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d WHERE d.Status='Signed')
  THROW 51001,'Signed clinical records are immutable. Create an authorized addendum.',1;
END;
GO
CREATE TRIGGER emr.ProtectPrescriptionItems ON emr.PrescriptionItems AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN emr.Encounters e ON e.Id=d.EncounterId WHERE e.Status='Signed')
  THROW 51002,'Signed prescription items are immutable.',1;
END;
GO
CREATE TRIGGER emr.ProtectAllergies ON emr.Allergies AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN emr.Encounters e ON e.Id=d.EncounterId WHERE e.Status='Signed')
  THROW 51003,'Signed allergy reconciliation is immutable.',1;
END;
GO
CREATE TRIGGER emr.ProtectOrders ON emr.Orders AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN emr.Encounters e ON e.Id=d.EncounterId WHERE e.Status='Signed')
  THROW 51004,'Signed clinical orders are immutable.',1;
END;
GO
CREATE TRIGGER bill.ProtectIssuedInvoice ON bill.Invoices AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE d.Status<>'Draft' AND (i.PatientId<>d.PatientId OR i.EncounterId<>d.EncounterId OR i.Discount<>d.Discount OR i.TaxRate<>d.TaxRate OR i.Number<>d.Number OR i.CreatedAtUtc<>d.CreatedAtUtc))
  THROW 51005,'Issued invoice financial content is immutable.',1;
 IF EXISTS(SELECT 1 FROM deleted d JOIN inserted i ON i.Id=d.Id WHERE NOT ((d.Status='Draft' AND i.Status IN ('Draft','Issued')) OR (d.Status='Issued' AND i.Status IN ('Part paid','Paid')) OR (d.Status='Part paid' AND i.Status='Paid')))
  THROW 51011,'Invalid issued-invoice state transition.',1;
 IF EXISTS(SELECT 1 FROM deleted d WHERE d.Status<>'Draft' AND NOT EXISTS(SELECT 1 FROM inserted i WHERE i.Id=d.Id))
  THROW 51006,'Issued invoices cannot be deleted.',1;
END;
GO
CREATE TRIGGER bill.ProtectLines ON bill.Lines AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51007,'Invoice lines are immutable after capture. Use a credit-note workflow.',1; END;
GO
CREATE TRIGGER bill.ProtectPayments ON bill.Payments AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51008,'Payments are append-only. Use an approved refund workflow.',1; END;
GO
CREATE TRIGGER audit.AppendOnly ON audit.AuditEvents AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51009,'Audit events are append-only.',1; END;
GO
CREATE TRIGGER emr.AppendOnlyVersions ON emr.RecordVersions AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 51010,'Clinical record versions are append-only.',1; END;
