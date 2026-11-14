-- Careflow Phase 2: IPD/ADT, nursing, pharmacy and LIS. SQL Server 2022+.
-- Forward migration; apply through Careflow.Api migrate. No synthetic patient data is seeded.
SET XACT_ABORT ON;
GO
CREATE SCHEMA ipd;
GO
CREATE SCHEMA nurse;
GO
CREATE SCHEMA pharmacy;
GO
CREATE SCHEMA lab;
GO
CREATE SCHEMA alerts;
GO
CREATE TABLE ipd.Beds (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Ward nvarchar(120) NOT NULL, Number varchar(30) NOT NULL, Kind varchar(30) NOT NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Available','Occupied','Cleaning','Maintenance')),
 Notes nvarchar(500) NOT NULL DEFAULT '', Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(BranchId,Ward,Number), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE ipd.Admissions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(40) NOT NULL, PatientId uniqueidentifier NOT NULL, BedId uniqueidentifier NOT NULL, DoctorId uniqueidentifier NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Active','Discharged')), Reason nvarchar(1000) NOT NULL, AdmittedAtUtc datetime2(3) NOT NULL,
 DischargedAtUtc datetime2(3) NULL, DischargeSummary nvarchar(max) NOT NULL DEFAULT '', DischargeDiagnosis nvarchar(1000) NOT NULL DEFAULT '', DischargeInstructions nvarchar(max) NOT NULL DEFAULT '',
 CreatedBy uniqueidentifier NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,BedId) REFERENCES ipd.Beds(HospitalId,Id), FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id), CHECK(DischargedAtUtc IS NULL OR DischargedAtUtc>=AdmittedAtUtc)
);
CREATE UNIQUE INDEX UX_Admissions_ActivePatient ON ipd.Admissions(HospitalId,PatientId) WHERE Status='Active';
CREATE UNIQUE INDEX UX_Admissions_ActiveBed ON ipd.Admissions(BedId) WHERE Status='Active';
CREATE TABLE ipd.BedEvents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NULL, BedId uniqueidentifier NOT NULL,
 Action varchar(40) NOT NULL, FromStatus varchar(20) NOT NULL, ToStatus varchar(20) NOT NULL, ActorId uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL, Reason nvarchar(1000) NOT NULL,
 FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,BedId) REFERENCES ipd.Beds(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE nurse.NursingNotes (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 Kind varchar(30) NOT NULL, Text nvarchar(max) NOT NULL, TemperatureC decimal(5,2) NULL, Pulse int NULL, Systolic int NULL, Diastolic int NULL, WeightKg decimal(6,2) NULL, Spo2 int NULL,
 IntakeMl int NULL, OutputMl int NULL, PainScore int NULL CHECK(PainScore BETWEEN 0 AND 10), FallRisk varchar(20) NOT NULL, CreatedBy uniqueidentifier NOT NULL, CreatedAtUtc datetime2(3) NOT NULL,
 FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE nurse.MedicationOrders (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 MedicineId uniqueidentifier NOT NULL, Name nvarchar(200) NOT NULL, Dose nvarchar(80) NOT NULL, Route nvarchar(40) NOT NULL, Frequency nvarchar(80) NOT NULL,
 StartsAtUtc datetime2(3) NOT NULL, EndsAtUtc datetime2(3) NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Active','Stopped')), OrderedBy uniqueidentifier NOT NULL, CreatedAtUtc datetime2(3) NOT NULL, Version bigint NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id),
 FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,MedicineId) REFERENCES catalog.Medicines(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE pharmacy.StockBatches (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, MedicineId uniqueidentifier NOT NULL,
 BatchNumber nvarchar(80) NOT NULL, ExpiryDate date NOT NULL, Quantity decimal(18,3) NOT NULL CHECK(Quantity>=0), UnitCost decimal(19,4) NOT NULL CHECK(UnitCost>=0),
 Supplier nvarchar(160) NOT NULL, Reference nvarchar(100) NOT NULL, ReceivedAtUtc datetime2(3) NOT NULL, ReceivedBy uniqueidentifier NOT NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Active','Depleted','Quarantined')), Version bigint NOT NULL DEFAULT 1,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,MedicineId,BatchNumber,Supplier), FOREIGN KEY(HospitalId,MedicineId) REFERENCES catalog.Medicines(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE nurse.Administrations (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, OrderId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NOT NULL,
 BatchId uniqueidentifier NOT NULL, Quantity decimal(18,3) NOT NULL CHECK(Quantity>0), GivenAtUtc datetime2(3) NOT NULL, GivenBy uniqueidentifier NOT NULL, Witness nvarchar(160) NOT NULL DEFAULT '', Notes nvarchar(1000) NOT NULL DEFAULT '',
 UNIQUE(OrderId,GivenAtUtc), FOREIGN KEY(HospitalId,OrderId) REFERENCES nurse.MedicationOrders(HospitalId,Id), FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,BatchId) REFERENCES pharmacy.StockBatches(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE nurse.ShiftHandovers (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, FromNurse uniqueidentifier NOT NULL, ToNurseId uniqueidentifier NOT NULL, ToNurseName nvarchar(160) NOT NULL,
 Summary nvarchar(max) NOT NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Pending','Acknowledged')), CreatedAtUtc datetime2(3) NOT NULL, AcknowledgedAtUtc datetime2(3) NULL, AcknowledgedBy uniqueidentifier NULL, Version bigint NOT NULL DEFAULT 1,
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id), FOREIGN KEY(HospitalId,ToNurseId) REFERENCES iam.Users(HospitalId,Id), FOREIGN KEY(HospitalId,FromNurse) REFERENCES iam.Users(HospitalId,Id), CHECK(Status='Pending' OR AcknowledgedBy IS NOT NULL), CHECK(FromNurse<>ToNurseId)
);
CREATE TABLE nurse.HandoverPatients (
 HandoverId uniqueidentifier NOT NULL REFERENCES nurse.ShiftHandovers(Id), HospitalId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL, PRIMARY KEY(HandoverId,PatientId),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
CREATE TABLE pharmacy.StockMovements (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, BatchId uniqueidentifier NOT NULL, Kind varchar(30) NOT NULL,
 Quantity decimal(18,3) NOT NULL, Reference nvarchar(100) NOT NULL, ActorId uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL,
 FOREIGN KEY(HospitalId,BatchId) REFERENCES pharmacy.StockBatches(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE pharmacy.Dispenses (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, Number varchar(40) NOT NULL,
 Kind varchar(10) NOT NULL CHECK(Kind IN ('OPD','IPD')), PatientId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NULL, AdmissionId uniqueidentifier NULL,
 IdempotencyKey varchar(100) NOT NULL, RequestHash char(64) NOT NULL, CreatedAtUtc datetime2(3) NOT NULL, CreatedBy uniqueidentifier NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,BranchId,CreatedBy,IdempotencyKey), FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id), FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE pharmacy.DispenseLines (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, DispenseId uniqueidentifier NOT NULL, MedicineId uniqueidentifier NOT NULL,
 Name nvarchar(200) NOT NULL, Quantity decimal(18,3) NOT NULL CHECK(Quantity>0), FOREIGN KEY(HospitalId,DispenseId) REFERENCES pharmacy.Dispenses(HospitalId,Id), FOREIGN KEY(HospitalId,MedicineId) REFERENCES catalog.Medicines(HospitalId,Id)
);
CREATE TABLE pharmacy.DispenseBatches (
 DispenseLineId uniqueidentifier NOT NULL REFERENCES pharmacy.DispenseLines(Id), BatchId uniqueidentifier NOT NULL, HospitalId uniqueidentifier NOT NULL, BatchNumber nvarchar(80) NOT NULL, Quantity decimal(18,3) NOT NULL CHECK(Quantity>0),
 PRIMARY KEY(DispenseLineId,BatchId), FOREIGN KEY(HospitalId,BatchId) REFERENCES pharmacy.StockBatches(HospitalId,Id)
);
CREATE TABLE lab.Tests (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, Code varchar(40) NOT NULL, Name nvarchar(180) NOT NULL,
 SpecimenType varchar(60) NOT NULL, Unit varchar(40) NOT NULL, ReferenceLow decimal(18,4) NULL, ReferenceHigh decimal(18,4) NULL, CriticalLow decimal(18,4) NULL, CriticalHigh decimal(18,4) NULL,
 Active bit NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code), CHECK(ReferenceLow IS NULL OR ReferenceHigh IS NULL OR ReferenceLow<=ReferenceHigh)
);
CREATE TABLE lab.Orders (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, Number varchar(40) NOT NULL,
 PatientId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NULL, AdmissionId uniqueidentifier NULL, TestId uniqueidentifier NOT NULL, Priority varchar(10) NOT NULL CHECK(Priority IN ('Routine','Urgent','STAT')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Ordered','Collected','Processing','Resulted','Validated','Authorized','Rejected','Cancelled')), Barcode varchar(60) NULL, Accession varchar(60) NULL,
 OrderedBy uniqueidentifier NOT NULL, OrderedAtUtc datetime2(3) NOT NULL, CollectedAtUtc datetime2(3) NULL, ProcessedAtUtc datetime2(3) NULL, ResultedAtUtc datetime2(3) NULL, ValidatedAtUtc datetime2(3) NULL, AuthorizedAtUtc datetime2(3) NULL,
 RejectionReason nvarchar(500) NOT NULL DEFAULT '', ClinicalNote nvarchar(1000) NOT NULL DEFAULT '', SpecimenType varchar(60) NULL, VolumeMl decimal(18,3) NULL, CollectedBy uniqueidentifier NULL, Version bigint NOT NULL DEFAULT 1, UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,Barcode), UNIQUE(HospitalId,Accession),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,EncounterId) REFERENCES emr.Encounters(HospitalId,Id), FOREIGN KEY(HospitalId,AdmissionId) REFERENCES ipd.Admissions(HospitalId,Id), FOREIGN KEY(HospitalId,TestId) REFERENCES lab.Tests(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE TABLE lab.Results (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, OrderId uniqueidentifier NOT NULL, Version int NOT NULL,
 Value nvarchar(500) NOT NULL, NumericValue decimal(18,4) NULL, Unit varchar(40) NOT NULL, ReferenceLow decimal(18,4) NULL, ReferenceHigh decimal(18,4) NULL, Flag varchar(30) NOT NULL,
 EnteredBy uniqueidentifier NOT NULL, EnteredAtUtc datetime2(3) NOT NULL, ValidatedBy uniqueidentifier NULL, ValidatedAtUtc datetime2(3) NULL, AuthorizedBy uniqueidentifier NULL, AuthorizedAtUtc datetime2(3) NULL, ContentHash char(64) NULL,
 UNIQUE(OrderId,Version), FOREIGN KEY(HospitalId,OrderId) REFERENCES lab.Orders(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id), CHECK(AuthorizedAtUtc IS NULL OR ContentHash IS NOT NULL)
);
CREATE TABLE alerts.CriticalAlerts (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, Kind varchar(20) NOT NULL,
 EntityId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL, Message nvarchar(1000) NOT NULL, Severity varchar(20) NOT NULL, Status varchar(20) NOT NULL CHECK(Status IN ('Open','Acknowledged')),
 CreatedAtUtc datetime2(3) NOT NULL, AcknowledgedAtUtc datetime2(3) NULL, AcknowledgedBy uniqueidentifier NULL, AcknowledgeNote nvarchar(1000) NOT NULL DEFAULT '',
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
CREATE INDEX IX_LabOrders_Worklist ON lab.Orders(HospitalId,BranchId,Status,OrderedAtUtc DESC);
CREATE INDEX IX_NursingNotes_Admission ON nurse.NursingNotes(HospitalId,BranchId,AdmissionId,CreatedAtUtc DESC);
CREATE INDEX IX_MedicationOrders_Admission ON nurse.MedicationOrders(HospitalId,BranchId,AdmissionId,Status);
CREATE INDEX IX_Administrations_Order ON nurse.Administrations(HospitalId,BranchId,OrderId,GivenAtUtc DESC);
CREATE INDEX IX_BedEvents_Bed ON ipd.BedEvents(HospitalId,BranchId,BedId,AtUtc DESC);
CREATE INDEX IX_StockMovements_Batch ON pharmacy.StockMovements(HospitalId,BranchId,BatchId,AtUtc DESC);
CREATE INDEX IX_LabResults_Order ON lab.Results(HospitalId,BranchId,OrderId,Version DESC);
CREATE INDEX IX_Stock_Expiry ON pharmacy.StockBatches(HospitalId,BranchId,ExpiryDate,Status);
CREATE INDEX IX_Alerts_Open ON alerts.CriticalAlerts(HospitalId,BranchId,Status,CreatedAtUtc DESC);
GO
INSERT INTO iam.Permissions([Key],Description) VALUES
('ipd.read','Read inpatient admissions and beds'),('ipd.write','Admit and transfer patients'),('ipd.discharge','Sign discharge summaries'),('ipd.bed','Manage bed readiness'),
('nursing.read','Read nursing records'),('nursing.write','Record nursing assessments and notes'),('nursing.order','Create/stop inpatient medication orders'),('nursing.administer','Record eMAR administrations'),('nursing.handover','Create/acknowledge shift handovers'),
('pharmacy.read','Read pharmacy stock and dispensing'),('pharmacy.write','Receive pharmacy stock'),('pharmacy.dispense','Dispense against signed prescriptions/admissions'),
('lab.read','Read laboratory worklists and reports'),('lab.order','Order laboratory tests'),('lab.collect','Collect/reject specimens'),('lab.process','Accession/process specimens'),('lab.result','Enter laboratory results'),('lab.validate','Technically validate results'),('lab.authorize','Authorize laboratory reports'),('lab.acknowledge','Acknowledge critical alerts');
GO
-- Configurable nursing escalation thresholds. ApprovedBy must be set by the hospital's
-- governed policy-approval process before nursing observations can be recorded.
INSERT INTO org.Policies(HospitalId,BranchId,[Key],Version,PayloadJson,EffectiveFromUtc,ApprovedBy,Reason)
SELECT h.Id,b.Id,'nursing.escalation',1,
 N'{"spo2Below":90,"systolicBelow":90,"systolicAbove":180,"pulseBelow":50,"pulseAbove":130,"temperatureAtOrAbove":39.5,"painAtOrAbove":8}',
 SYSUTCDATETIME(),NULL,'Default thresholds require hospital approval before use.'
FROM org.Hospitals h JOIN org.Branches b ON b.HospitalId=h.Id;
GO
CREATE TRIGGER ipd.ProtectDischarged ON ipd.Admissions AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status='Discharged') THROW 52001,'Discharged admissions are immutable. Use an approved amendment workflow.',1; END;
GO
CREATE TRIGGER nurse.AppendOnlyNotes ON nurse.NursingNotes AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 52002,'Nursing notes are append-only.',1; END;
GO
CREATE TRIGGER nurse.AppendOnlyAdministrations ON nurse.Administrations AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 52003,'Medication administrations are append-only.',1; END;
GO
CREATE TRIGGER pharmacy.AppendOnlyMovements ON pharmacy.StockMovements AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 52004,'Stock movements are append-only.',1; END;
GO
CREATE TRIGGER pharmacy.AppendOnlyDispenses ON pharmacy.Dispenses AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 52005,'Dispenses are append-only. Use an approved return workflow.',1; END;
GO
CREATE TRIGGER lab.ProtectAuthorizedOrders ON lab.Orders AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Authorized','Rejected')) THROW 52006,'Authorized/rejected lab orders are immutable.',1; END;
GO
CREATE TRIGGER lab.ProtectAuthorizedResults ON lab.Results AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE AuthorizedAtUtc IS NOT NULL) THROW 52007,'Authorized lab results are immutable. Issue an addendum/corrected report.',1; END;
GO
CREATE TRIGGER alerts.ProtectAcknowledged ON alerts.CriticalAlerts AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status='Acknowledged') THROW 52008,'Acknowledged alerts are immutable.',1; END;
