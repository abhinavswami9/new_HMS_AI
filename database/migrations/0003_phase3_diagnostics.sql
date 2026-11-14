-- Careflow Phase 3: radiology/RIS, emergency, operation theatre and blood bank.
-- Phase 3 of the delivery plan (docs/ROADMAP.md). PACS/DICOM remains an explicit
-- integration boundary: study rows record the StudyInstanceUID and status only, and no
-- live modality, archive or diagnostic viewer connection is claimed.
CREATE SCHEMA ris;
GO
CREATE SCHEMA emergency;
GO
CREATE SCHEMA ot;
GO
CREATE SCHEMA blood;
GO
CREATE TABLE ris.Orders (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(40) NOT NULL, PatientId uniqueidentifier NOT NULL, EncounterId uniqueidentifier NULL, AdmissionId uniqueidentifier NULL,
 ProcedureName nvarchar(200) NOT NULL, Modality varchar(30) NOT NULL CHECK(Modality IN ('X-ray','Ultrasound','CT','MRI')),
 BodyPart nvarchar(60) NOT NULL, Laterality varchar(20) NOT NULL CHECK(Laterality IN ('Left','Right','Bilateral','Not applicable')),
 Priority varchar(20) NOT NULL CHECK(Priority IN ('Routine','Urgent','STAT')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Ordered','Scheduled','Performed','Reported','Authorized','Cancelled')),
 Accession varchar(40) NULL, ClinicalNote nvarchar(1000) NOT NULL,
 OrderedBy uniqueidentifier NOT NULL, OrderedAtUtc datetime2(3) NOT NULL, ScheduledAtUtc datetime2(3) NULL, PerformedAtUtc datetime2(3) NULL,
 StudyUid varchar(64) NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,Accession),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id),
 CHECK(ScheduledAtUtc IS NULL OR ScheduledAtUtc>=OrderedAtUtc), CHECK(PerformedAtUtc IS NULL OR ScheduledAtUtc IS NOT NULL),
 CHECK(StudyUid IS NULL OR (LEN(StudyUid) BETWEEN 10 AND 64))
);
GO
CREATE TABLE ris.Reports (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, OrderId uniqueidentifier NOT NULL,
 Version int NOT NULL CHECK(Version>0), Findings nvarchar(max) NOT NULL, Impression nvarchar(2000) NOT NULL, Critical bit NOT NULL DEFAULT 0,
 Status varchar(20) NOT NULL CHECK(Status IN ('Reported','Authorized')),
 ReportedBy uniqueidentifier NOT NULL, ReportedAtUtc datetime2(3) NOT NULL,
 AuthorizedBy uniqueidentifier NULL, AuthorizedAtUtc datetime2(3) NULL, ContentHash char(64) NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,OrderId), FOREIGN KEY(HospitalId,OrderId) REFERENCES ris.Orders(HospitalId,Id),
 CHECK(AuthorizedAtUtc IS NULL OR Status='Authorized'), CHECK((ContentHash IS NULL) = (AuthorizedAtUtc IS NULL))
);
GO
CREATE TABLE emergency.Registrations (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(40) NOT NULL, PatientId uniqueidentifier NULL, PatientName nvarchar(160) NOT NULL,
 Sex varchar(20) NOT NULL CHECK(Sex IN ('Male','Female','Other','Unknown')), DateOfBirth date NULL,
 ArrivalMode varchar(20) NOT NULL CHECK(ArrivalMode IN ('Walk-in','Ambulance','Police','Referral','Other')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Open','Dispositioned')), ArrivedAtUtc datetime2(3) NOT NULL, CreatedBy uniqueidentifier NOT NULL,
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id),
 CHECK(PatientId IS NULL OR DateOfBirth IS NULL OR DateOfBirth<=CAST(ArrivedAtUtc AS date))
);
GO
CREATE TABLE emergency.TriageAssessments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, RegistrationId uniqueidentifier NOT NULL,
 Category varchar(20) NOT NULL CHECK(Category IN ('Red','Yellow','Green','Black')), Complaint nvarchar(1000) NOT NULL,
 TemperatureC decimal(4,1) NULL, Pulse int NULL, Systolic int NULL, Diastolic int NULL, WeightKg decimal(5,2) NULL, Spo2 int NULL,
 AssessedBy uniqueidentifier NOT NULL, AssessedAtUtc datetime2(3) NOT NULL,
 FOREIGN KEY(HospitalId,RegistrationId) REFERENCES emergency.Registrations(HospitalId,Id)
);
GO
CREATE TABLE emergency.Observations (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, RegistrationId uniqueidentifier NOT NULL,
 Note nvarchar(2000) NOT NULL,
 TemperatureC decimal(4,1) NULL, Pulse int NULL, Systolic int NULL, Diastolic int NULL, WeightKg decimal(5,2) NULL, Spo2 int NULL,
 RecordedBy uniqueidentifier NOT NULL, RecordedAtUtc datetime2(3) NOT NULL,
 FOREIGN KEY(HospitalId,RegistrationId) REFERENCES emergency.Registrations(HospitalId,Id)
);
GO
CREATE TABLE emergency.Dispositions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, RegistrationId uniqueidentifier NOT NULL,
 Kind varchar(20) NOT NULL CHECK(Kind IN ('Admit','Discharge','Refer','LAMA')), Summary nvarchar(5000) NOT NULL,
 AdmissionId uniqueidentifier NULL, ReceivingFacility nvarchar(200) NULL,
 DecidedBy uniqueidentifier NOT NULL, DecidedAtUtc datetime2(3) NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,RegistrationId),
 FOREIGN KEY(HospitalId,RegistrationId) REFERENCES emergency.Registrations(HospitalId,Id),
 CHECK((Kind='Refer') = (ReceivingFacility IS NOT NULL)), CHECK((Kind='Admit') = (AdmissionId IS NOT NULL))
);
GO
CREATE TABLE ot.Bookings (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(40) NOT NULL, PatientId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NULL,
 ProcedureName nvarchar(200) NOT NULL, Laterality varchar(20) NOT NULL CHECK(Laterality IN ('Left','Right','Bilateral','Not applicable')),
 Site nvarchar(120) NOT NULL, SurgeonId uniqueidentifier NOT NULL,
 Anaesthesia varchar(20) NOT NULL CHECK(Anaesthesia IN ('General','Regional','Local','Sedation')),
 Priority varchar(20) NOT NULL CHECK(Priority IN ('Elective','Urgent','Emergency')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Requested','Scheduled','In Theatre','Recovery','Completed','Cancelled')),
 Theatre nvarchar(40) NOT NULL DEFAULT '', ScheduledStartUtc datetime2(3) NULL, ScheduledEndUtc datetime2(3) NULL,
 RequestedAtUtc datetime2(3) NOT NULL, RequestedBy uniqueidentifier NOT NULL,
 TimeInUtc datetime2(3) NULL, TimeOutUtc datetime2(3) NULL, OperationNote nvarchar(max) NOT NULL DEFAULT '',
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,SurgeonId) REFERENCES catalog.Practitioners(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id),
 CHECK(ScheduledEndUtc IS NULL OR ScheduledStartUtc IS NULL OR ScheduledEndUtc>ScheduledStartUtc),
 CHECK(TimeInUtc IS NULL OR ScheduledStartUtc IS NOT NULL), CHECK(TimeOutUtc IS NULL OR (TimeInUtc IS NOT NULL AND TimeOutUtc>=TimeInUtc))
);
GO
CREATE TABLE ot.Checklists (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BookingId uniqueidentifier NOT NULL,
 Consent bit NOT NULL, AnaesthesiaAssessment bit NOT NULL, FastingConfirmed bit NOT NULL, SiteMarked bit NOT NULL, SafetyChecklist bit NOT NULL,
 Notes nvarchar(1000) NOT NULL DEFAULT '', RecordedBy uniqueidentifier NOT NULL, RecordedAtUtc datetime2(3) NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,BookingId),
 FOREIGN KEY(HospitalId,BookingId) REFERENCES ot.Bookings(HospitalId,Id),
 CHECK(Consent=1 AND AnaesthesiaAssessment=1 AND FastingConfirmed=1 AND SiteMarked=1 AND SafetyChecklist=1)
);
GO
CREATE TABLE blood.Units (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 UnitNumber varchar(40) NOT NULL,
 BloodGroup varchar(4) NOT NULL CHECK(BloodGroup IN ('O-','O+','A-','A+','B-','B+','AB-','AB+')),
 Component varchar(20) NOT NULL CHECK(Component IN ('WholeBlood','PRBC','FFP','Platelets')),
 ExpiryDate date NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Quarantined','Available','Reserved','Issued','Transfused','Discarded')),
 DonorName nvarchar(160) NOT NULL, CollectedAtUtc datetime2(3) NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,UnitNumber), FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE TABLE blood.ScreeningTests (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, UnitId uniqueidentifier NOT NULL,
 Hbsag bit NOT NULL, Hiv bit NOT NULL, Hcv bit NOT NULL, Vdrl bit NOT NULL,
 Notes nvarchar(500) NOT NULL DEFAULT '', TestedBy uniqueidentifier NOT NULL, TestedAtUtc datetime2(3) NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,UnitId), FOREIGN KEY(HospitalId,UnitId) REFERENCES blood.Units(HospitalId,Id)
);
GO
CREATE TABLE blood.Crossmatches (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, Number varchar(40) NOT NULL,
 UnitId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 Compatibility varchar(20) NOT NULL CHECK(Compatibility IN ('Compatible','Incompatible')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Matched','Consumed')),
 TestedBy uniqueidentifier NOT NULL, TestedAtUtc datetime2(3) NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,UnitId) REFERENCES blood.Units(HospitalId,Id),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id)
);
GO
CREATE TABLE blood.Issues (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, Number varchar(40) NOT NULL,
 UnitId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL, AdmissionId uniqueidentifier NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Issued','Transfused','Returned')),
 Reaction nvarchar(1000) NOT NULL DEFAULT '', ReturnAssessment nvarchar(200) NOT NULL DEFAULT '',
 IssuedBy uniqueidentifier NOT NULL, IssuedAtUtc datetime2(3) NOT NULL, TransfusedAtUtc datetime2(3) NULL,
 Notes nvarchar(500) NOT NULL DEFAULT '', Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,UnitId) REFERENCES blood.Units(HospitalId,Id),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 CHECK(TransfusedAtUtc IS NULL OR Status='Transfused')
);
GO
INSERT INTO iam.Permissions([Key],Description) VALUES
('radiology.read','Read radiology orders, worklists and reports'),('radiology.order','Order radiology studies'),('radiology.schedule','Schedule studies and assign accessions'),
('radiology.perform','Record study acquisition and Study UIDs'),('radiology.report','Record structured radiology reports'),('radiology.authorize','Authorize final radiology reports'),
('emergency.read','Read the emergency board and episodes'),('emergency.write','Register emergency presentations and record observations'),
('emergency.triage','Record triage assessments'),('emergency.dispose','Decide emergency dispositions'),
('ot.read','Read theatre bookings and schedules'),('ot.request','Request surgical bookings'),('ot.schedule','Schedule theatres and manage cancellations'),('ot.record','Record checklists, times and operation notes'),
('blood.read','Read blood bank inventory and issues'),('blood.receive','Receive blood units'),('blood.screen','Record screening results and dispose units'),
('blood.crossmatch','Perform crossmatches'),('blood.issue','Issue and return blood units'),('blood.transfuse','Record transfusions and reactions');
GO
-- Defense in depth: final imaging reports, emergency assessments, surgical safety
-- records, screening results and closed blood issues are immutable once finalized.
CREATE TRIGGER ris.ProtectAuthorizedReport ON ris.Reports AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status='Authorized') THROW 53001,'Authorized radiology reports are immutable.',1; END;
GO
CREATE TRIGGER emergency.AppendOnlyTriage ON emergency.TriageAssessments AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 53002,'Triage assessments are append-only.',1; END;
GO
CREATE TRIGGER emergency.AppendOnlyObservations ON emergency.Observations AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 53003,'Emergency observations are append-only.',1; END;
GO
CREATE TRIGGER blood.ProtectClosedIssues ON blood.Issues AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Transfused','Returned')) THROW 53004,'Closed blood issues are immutable.',1; END;
GO
CREATE TRIGGER ot.ProtectCompletedBooking ON ot.Bookings AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Completed','Cancelled')) THROW 53005,'Completed or cancelled surgeries are immutable.',1; END;
GO
CREATE TRIGGER blood.ProtectScreening ON blood.ScreeningTests AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 53006,'Screening results are append-only.',1; END;
GO
CREATE INDEX IX_RisOrders_Worklist ON ris.Orders(HospitalId,BranchId,Status,OrderedAtUtc DESC);
CREATE INDEX IX_EmergencyBoard ON emergency.Registrations(HospitalId,BranchId,Status,ArrivedAtUtc DESC);
CREATE INDEX IX_OtSchedule ON ot.Bookings(HospitalId,BranchId,Status,ScheduledStartUtc);
CREATE INDEX IX_OtTheatre ON ot.Bookings(HospitalId,Theatre,ScheduledStartUtc);
CREATE INDEX IX_BloodInventory ON blood.Units(HospitalId,BranchId,Status,ExpiryDate);
GO
