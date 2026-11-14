-- Careflow Phase 4: insurance/TPA, doctor incentives, telemedicine, inventory and CSSD.
-- Phase 4 of the delivery plan (docs/ROADMAP.md). Integration boundaries stay explicit:
-- no live payer gateway, video provider, payment service or procurement marketplace is
-- contacted. Telemedicine rows record consent, waiting-room and consultation metadata
-- only; join credentials are ephemeral application tokens, not video-room links.
CREATE SCHEMA insurance;
GO
CREATE SCHEMA incentive;
GO
CREATE SCHEMA tele;
GO
CREATE SCHEMA inventory;
GO
CREATE SCHEMA cssd;
GO
CREATE TABLE insurance.Payers (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Code varchar(30) NOT NULL, Name nvarchar(120) NOT NULL, Contact nvarchar(120) NOT NULL DEFAULT '', Active bit NOT NULL DEFAULT 1,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code)
);
GO
CREATE TABLE insurance.Plans (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 PayerId uniqueidentifier NOT NULL, Code varchar(30) NOT NULL, Name nvarchar(120) NOT NULL, PolicyType nvarchar(40) NOT NULL,
 SumInsuredLimit decimal(19,4) NOT NULL CHECK(SumInsuredLimit>0),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,PayerId) REFERENCES insurance.Payers(HospitalId,Id)
);
GO
CREATE TABLE insurance.Coverages (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, PatientId uniqueidentifier NOT NULL, PlanId uniqueidentifier NOT NULL,
 PolicyNumber varchar(40) NOT NULL, MemberId varchar(40) NOT NULL, ValidFromDate date NOT NULL, ValidToDate date NOT NULL,
 SumInsured decimal(19,4) NOT NULL CHECK(SumInsured>0), Deductible decimal(19,4) NOT NULL CHECK(Deductible>=0),
 Status varchar(20) NOT NULL CHECK(Status IN ('Active','Expired','Cancelled')), Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,PatientId,PolicyNumber),
 CHECK(ValidToDate>=ValidFromDate), CHECK(Deductible<SumInsured),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,PlanId) REFERENCES insurance.Plans(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE TABLE insurance.Preauthorizations (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, CoverageId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL,
 DiagnosisCode varchar(20) NOT NULL, ProcedureCode varchar(20) NOT NULL, RequestedAmount decimal(19,4) NOT NULL CHECK(RequestedAmount>0),
 Status varchar(20) NOT NULL CHECK(Status IN ('Requested','Approved','Rejected')), ValidFromDate date NOT NULL, ValidToDate date NOT NULL,
 ApprovedAmount decimal(19,4) NULL CHECK(ApprovedAmount IS NULL OR (ApprovedAmount>0 AND ApprovedAmount<=RequestedAmount)),
 ResponseNote nvarchar(500) NOT NULL DEFAULT '', RespondedBy uniqueidentifier NULL, RespondedAtUtc datetime2(3) NULL,
 RequestedBy uniqueidentifier NOT NULL, RequestedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,CoverageId) REFERENCES insurance.Coverages(HospitalId,Id),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 CHECK((Status='Requested' AND ApprovedAmount IS NULL) OR (Status<>'Requested' AND RespondedBy IS NOT NULL AND RespondedAtUtc IS NOT NULL))
);
GO
CREATE TABLE insurance.Claims (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, PatientId uniqueidentifier NOT NULL, CoverageId uniqueidentifier NOT NULL,
 InvoiceId uniqueidentifier NULL, AdmissionId uniqueidentifier NULL, DiagnosisCode varchar(20) NOT NULL,
 TotalAmount decimal(19,4) NOT NULL CHECK(TotalAmount>0), ApprovedAmount decimal(19,4) NOT NULL DEFAULT 0 CHECK(ApprovedAmount>=0),
 PatientLiability decimal(19,4) NOT NULL DEFAULT 0 CHECK(PatientLiability>=0),
 Status varchar(20) NOT NULL CHECK(Status IN ('Draft','Submitted','Queried','Responded','Approved','Rejected','Settled')),
 QueryNote nvarchar(1000) NOT NULL DEFAULT '', ResponseNote nvarchar(2000) NOT NULL DEFAULT '', RejectionReason nvarchar(500) NOT NULL DEFAULT '',
 SubmittedBy uniqueidentifier NULL, SubmittedAtUtc datetime2(3) NULL, CreatedBy uniqueidentifier NOT NULL, CreatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,CoverageId) REFERENCES insurance.Coverages(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE UNIQUE INDEX UX_Claims_Invoice ON insurance.Claims(HospitalId,InvoiceId) WHERE InvoiceId IS NOT NULL;
GO
CREATE TABLE insurance.ClaimLines (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, ClaimId uniqueidentifier NOT NULL,
 ServiceCode varchar(30) NOT NULL, Description nvarchar(200) NOT NULL, Quantity decimal(19,4) NOT NULL CHECK(Quantity>0),
 UnitPrice decimal(19,4) NOT NULL CHECK(UnitPrice>=0), Amount decimal(19,4) NOT NULL CHECK(Amount>0), ApprovedAmount decimal(19,4) NOT NULL DEFAULT 0,
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,ClaimId) REFERENCES insurance.Claims(HospitalId,Id)
);
GO
CREATE TABLE insurance.ClaimQueries (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, ClaimId uniqueidentifier NOT NULL,
 Question nvarchar(1000) NOT NULL, Response nvarchar(2000) NULL, RaisedBy uniqueidentifier NOT NULL, RaisedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 RespondedBy uniqueidentifier NULL, RespondedAtUtc datetime2(3) NULL,
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,ClaimId) REFERENCES insurance.Claims(HospitalId,Id),
 CHECK((Response IS NULL AND RespondedAtUtc IS NULL) OR (Response IS NOT NULL AND RespondedAtUtc IS NOT NULL))
);
GO
CREATE TABLE insurance.Settlements (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, ClaimId uniqueidentifier NOT NULL, Amount decimal(19,4) NOT NULL CHECK(Amount>0),
 Mode varchar(20) NOT NULL CHECK(Mode IN ('Cash','Cheque','Bank transfer','Adjustment')),
 Reference varchar(60) NOT NULL, SettledAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), SettledBy uniqueidentifier NOT NULL,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,Reference),
 FOREIGN KEY(HospitalId,ClaimId) REFERENCES insurance.Claims(HospitalId,Id),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE TABLE incentive.Rules (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Name nvarchar(120) NOT NULL, ScopeKind varchar(10) NOT NULL CHECK(ScopeKind IN ('Doctor','All')), DoctorId uniqueidentifier NULL,
 Basis varchar(20) NOT NULL CHECK(Basis IN ('Consultation','Revenue','Surgery','Procedure')),
 Mode varchar(10) NOT NULL CHECK(Mode IN ('Fixed','Percent')), RuleValue decimal(19,4) NOT NULL CHECK(RuleValue>=0),
 EffectiveFromDate date NOT NULL, EffectiveToDate date NULL, Status varchar(10) NOT NULL CHECK(Status IN ('Draft','Active','Retired')),
 CreatedBy uniqueidentifier NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), CHECK(EffectiveToDate IS NULL OR EffectiveToDate>=EffectiveFromDate),
 CHECK((ScopeKind='Doctor' AND DoctorId IS NOT NULL) OR (ScopeKind='All' AND DoctorId IS NULL)),
 CHECK(Mode<>'Percent' OR Basis='Revenue'), CHECK(Mode<>'Percent' OR RuleValue<=100),
 FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id)
);
GO
CREATE TABLE incentive.Batches (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, PeriodStartDate date NOT NULL, PeriodEndDate date NOT NULL,
 Status varchar(10) NOT NULL CHECK(Status IN ('Calculated','Approved','Paid')), TotalAmount decimal(19,4) NOT NULL DEFAULT 0 CHECK(TotalAmount>=0),
 CalculatedBy nvarchar(160) NOT NULL, CalculatedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 ApprovedBy nvarchar(160) NOT NULL DEFAULT '', ApprovedAtUtc datetime2(3) NULL, RecalculationReason nvarchar(500) NOT NULL DEFAULT '',
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,PeriodStartDate,PeriodEndDate),
 CHECK(PeriodEndDate>=PeriodStartDate), CHECK((Status='Calculated' AND ApprovedAtUtc IS NULL) OR (Status<>'Calculated' AND ApprovedAtUtc IS NOT NULL)),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE TABLE incentive.Lines (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BatchId uniqueidentifier NOT NULL,
 DoctorId uniqueidentifier NOT NULL, RuleId uniqueidentifier NOT NULL, BasisLabel varchar(80) NOT NULL,
 BasisAmount decimal(19,4) NOT NULL DEFAULT 0 CHECK(BasisAmount>=0), IncentiveAmount decimal(19,4) NOT NULL CHECK(IncentiveAmount>=0),
 Snapshot nvarchar(500) NOT NULL DEFAULT '',
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,BatchId) REFERENCES incentive.Batches(HospitalId,Id),
 FOREIGN KEY(HospitalId,RuleId) REFERENCES incentive.Rules(HospitalId,Id),
 FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id)
);
GO
CREATE TABLE incentive.Payments (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BatchId uniqueidentifier NOT NULL,
 Amount decimal(19,4) NOT NULL CHECK(Amount>0), Mode varchar(20) NOT NULL CHECK(Mode IN ('Cash','Cheque','Bank transfer','Adjustment')),
 Reference varchar(60) NOT NULL, PaidBy uniqueidentifier NOT NULL, PaidAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Reference), FOREIGN KEY(HospitalId,BatchId) REFERENCES incentive.Batches(HospitalId,Id)
);
GO
CREATE TABLE tele.Sessions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, AppointmentId uniqueidentifier NOT NULL, PatientId uniqueidentifier NOT NULL, DoctorId uniqueidentifier NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Scheduled','Waiting','InConsultation','Completed','Cancelled')),
 ScheduledAtUtc datetime2(3) NOT NULL, ConsentObtained bit NOT NULL DEFAULT 0, StartedAtUtc datetime2(3) NULL, EndedAtUtc datetime2(3) NULL,
 EndNote nvarchar(2000) NOT NULL DEFAULT '', EmergencyAdvice nvarchar(1000) NOT NULL DEFAULT '',
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,AppointmentId),
 FOREIGN KEY(HospitalId,AppointmentId) REFERENCES sched.Appointments(HospitalId,Id),
 FOREIGN KEY(HospitalId,PatientId) REFERENCES patient.Patients(HospitalId,Id),
 FOREIGN KEY(HospitalId,DoctorId) REFERENCES catalog.Practitioners(HospitalId,Id),
 CHECK((Status IN ('Completed','Cancelled')) OR (StartedAtUtc IS NULL AND EndedAtUtc IS NULL) OR (Status='InConsultation' AND StartedAtUtc IS NOT NULL))
);
GO
CREATE TABLE tele.ConsentEvents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, SessionId uniqueidentifier NOT NULL,
 Kind varchar(30) NOT NULL CHECK(Kind IN ('PatientConsent','RecordingConsent')), Given bit NOT NULL, Note nvarchar(500) NOT NULL DEFAULT '',
 ActorId uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,SessionId) REFERENCES tele.Sessions(HospitalId,Id)
);
GO
CREATE TABLE tele.WaitingEvents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, SessionId uniqueidentifier NOT NULL,
 Kind varchar(20) NOT NULL CHECK(Kind IN ('JoinedWaiting','Admitted')), ActorId uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,SessionId) REFERENCES tele.Sessions(HospitalId,Id)
);
GO
CREATE TABLE tele.Uploads (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, SessionId uniqueidentifier NOT NULL,
 FileName nvarchar(120) NOT NULL, ContentType varchar(120) NOT NULL, SizeBytes bigint NOT NULL CHECK(SizeBytes>0 AND SizeBytes<=10485760),
 UploadedBy uniqueidentifier NOT NULL, UploadedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,SessionId) REFERENCES tele.Sessions(HospitalId,Id)
);
GO
CREATE TABLE inventory.Stores (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Code varchar(20) NOT NULL, Name nvarchar(120) NOT NULL, Kind varchar(20) NOT NULL CHECK(Kind IN ('Central','Department')),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code)
);
GO
CREATE TABLE inventory.Items (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Code varchar(30) NOT NULL, Name nvarchar(120) NOT NULL, Category varchar(20) NOT NULL CHECK(Category IN ('Consumable','Implant','Asset','Medicine')),
 Uom varchar(20) NOT NULL, ReorderLevel int NOT NULL DEFAULT 0 CHECK(ReorderLevel>=0), UnitCost decimal(19,4) NOT NULL CHECK(UnitCost>=0), Active bit NOT NULL DEFAULT 1,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code)
);
GO
CREATE TABLE inventory.Vendors (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Code varchar(20) NOT NULL, Name nvarchar(120) NOT NULL, Authorized bit NOT NULL DEFAULT 0,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code), UNIQUE(HospitalId,Name)
);
GO
CREATE TABLE inventory.Batches (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 BatchNumber varchar(40) NOT NULL, ItemId uniqueidentifier NOT NULL, StoreId uniqueidentifier NOT NULL, ExpiryDate date NULL,
 Quantity int NOT NULL DEFAULT 0 CHECK(Quantity>=0), UnitCost decimal(19,4) NOT NULL DEFAULT 0 CHECK(UnitCost>=0),
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,ItemId,BatchNumber,StoreId),
 FOREIGN KEY(HospitalId,ItemId) REFERENCES inventory.Items(HospitalId,Id),
 FOREIGN KEY(HospitalId,StoreId) REFERENCES inventory.Stores(HospitalId,Id)
);
GO
CREATE TABLE inventory.Movements (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 ItemId uniqueidentifier NOT NULL, StoreId uniqueidentifier NOT NULL, BatchId uniqueidentifier NULL,
 Kind varchar(20) NOT NULL CHECK(Kind IN ('Receipt','Transfer','Consumption','Adjustment')), Quantity int NOT NULL,
 RefType varchar(30) NOT NULL, RefId varchar(36) NOT NULL, RecordedBy uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id),
 FOREIGN KEY(HospitalId,ItemId) REFERENCES inventory.Items(HospitalId,Id),
 FOREIGN KEY(HospitalId,StoreId) REFERENCES inventory.Stores(HospitalId,Id)
);
GO
CREATE TABLE inventory.Indents (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, StoreId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL,
 Quantity int NOT NULL CHECK(Quantity>0), Priority varchar(20) NOT NULL CHECK(Priority IN ('Routine','Urgent','Emergency')),
 Status varchar(20) NOT NULL CHECK(Status IN ('Requested','Approved','Rejected','Ordered','Received')),
 Reason nvarchar(500) NOT NULL, RaisedBy uniqueidentifier NOT NULL, RaisedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 ApprovedBy uniqueidentifier NULL, ApprovedAtUtc datetime2(3) NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,StoreId) REFERENCES inventory.Stores(HospitalId,Id),
 FOREIGN KEY(HospitalId,ItemId) REFERENCES inventory.Items(HospitalId,Id),
 CHECK((Status='Requested' AND ApprovedBy IS NULL) OR (Status<>'Requested' AND ApprovedBy IS NOT NULL AND ApprovedAtUtc IS NOT NULL AND ApprovedBy<>RaisedBy))
);
GO
CREATE TABLE inventory.PurchaseOrders (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, VendorId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL, IndentId uniqueidentifier NOT NULL,
 Quantity int NOT NULL CHECK(Quantity>0), UnitCost decimal(19,4) NOT NULL CHECK(UnitCost>=0),
 Status varchar(20) NOT NULL CHECK(Status IN ('Issued','Received')), RaisedBy uniqueidentifier NOT NULL,
 RaisedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), ReceivedBy uniqueidentifier NULL, ReceivedAtUtc datetime2(3) NULL,
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,IndentId),
 FOREIGN KEY(HospitalId,VendorId) REFERENCES inventory.Vendors(HospitalId,Id),
 FOREIGN KEY(HospitalId,ItemId) REFERENCES inventory.Items(HospitalId,Id),
 FOREIGN KEY(HospitalId,IndentId) REFERENCES inventory.Indents(HospitalId,Id)
);
GO
CREATE TABLE inventory.GoodsReceipts (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, PurchaseOrderId uniqueidentifier NOT NULL, ReceivedQty int NOT NULL CHECK(ReceivedQty>0),
 AcceptedQty int NOT NULL CHECK(AcceptedQty>=0), RejectedQty int NOT NULL CHECK(RejectedQty>=0), InspectionNote nvarchar(500) NOT NULL DEFAULT '',
 ReceivedBy uniqueidentifier NOT NULL, ReceivedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), UNIQUE(HospitalId,PurchaseOrderId),
 CHECK(AcceptedQty+RejectedQty=ReceivedQty),
 FOREIGN KEY(HospitalId,PurchaseOrderId) REFERENCES inventory.PurchaseOrders(HospitalId,Id)
);
GO
CREATE TABLE inventory.Transfers (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, FromStoreId uniqueidentifier NOT NULL, ToStoreId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL,
 BatchId uniqueidentifier NOT NULL, Quantity int NOT NULL CHECK(Quantity>0),
 Status varchar(20) NOT NULL CHECK(Status IN ('Dispatched','Received')), Note nvarchar(500) NOT NULL DEFAULT '',
 DispatchedBy uniqueidentifier NOT NULL, DispatchedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 ReceivedBy uniqueidentifier NULL, ReceivedAtUtc datetime2(3) NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), CHECK(FromStoreId<>ToStoreId),
 FOREIGN KEY(HospitalId,FromStoreId) REFERENCES inventory.Stores(HospitalId,Id),
 FOREIGN KEY(HospitalId,ToStoreId) REFERENCES inventory.Stores(HospitalId,Id),
 FOREIGN KEY(HospitalId,BatchId) REFERENCES inventory.Batches(HospitalId,Id)
);
GO
CREATE TABLE inventory.Consumptions (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, StoreId uniqueidentifier NOT NULL, ItemId uniqueidentifier NOT NULL, BatchId uniqueidentifier NOT NULL,
 Quantity int NOT NULL CHECK(Quantity>0), Department nvarchar(60) NOT NULL, Reason nvarchar(500) NOT NULL,
 RecordedBy uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,BatchId) REFERENCES inventory.Batches(HospitalId,Id)
);
GO
CREATE TABLE inventory.Assets (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Tag varchar(60) NOT NULL, ItemId uniqueidentifier NOT NULL, SerialNumber varchar(60) NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('InStore','InUse','UnderMaintenance','Retired')),
 Custodian nvarchar(60) NOT NULL DEFAULT '', RecordedBy uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Tag), UNIQUE(HospitalId,SerialNumber),
 FOREIGN KEY(HospitalId,ItemId) REFERENCES inventory.Items(HospitalId,Id)
);
GO
CREATE TABLE cssd.Sets (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL,
 Code varchar(30) NOT NULL, Name nvarchar(80) NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Dirty','Cleaning','Packed','Sterilizing','Sterile','Issued','Quarantined')),
 MissingCount int NOT NULL DEFAULT 0 CHECK(MissingCount>=0), SterileExpiryUtc datetime2(3) NULL, LastCycleId uniqueidentifier NULL,
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Code)
);
GO
CREATE TABLE cssd.SetItems (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, SetId uniqueidentifier NOT NULL,
 Name nvarchar(60) NOT NULL, SerialNumber varchar(40) NOT NULL DEFAULT '', Condition varchar(20) NOT NULL CHECK(Condition IN ('Ok','Missing','Damaged')),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,SetId) REFERENCES cssd.Sets(HospitalId,Id)
);
GO
CREATE TABLE cssd.Cycles (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, Method varchar(20) NOT NULL CHECK(Method IN ('Steam','Ethylene oxide','Plasma')),
 Machine nvarchar(40) NOT NULL, TempC decimal(5,2) NOT NULL, DurationMin int NOT NULL CHECK(DurationMin BETWEEN 1 AND 240),
 Status varchar(10) NOT NULL CHECK(Status IN ('Running','Passed','Failed')), StartedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 EndedAtUtc datetime2(3) NULL, OperatorBy uniqueidentifier NOT NULL, Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 CHECK((Method='Steam' AND TempC BETWEEN 121 AND 134) OR (Method='Ethylene oxide' AND TempC BETWEEN 37 AND 63) OR (Method='Plasma' AND TempC BETWEEN 45 AND 65)),
 CHECK((Status='Running' AND EndedAtUtc IS NULL) OR (Status<>'Running' AND EndedAtUtc IS NOT NULL)),
 FOREIGN KEY(HospitalId,BranchId) REFERENCES org.Branches(HospitalId,Id)
);
GO
CREATE TABLE cssd.CycleLoads (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, CycleId uniqueidentifier NOT NULL,
 SetId uniqueidentifier NOT NULL, SetStatus varchar(20) NOT NULL CHECK(SetStatus IN ('Sterilizing','Sterile','Quarantined')),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,CycleId) REFERENCES cssd.Cycles(HospitalId,Id),
 FOREIGN KEY(HospitalId,SetId) REFERENCES cssd.Sets(HospitalId,Id)
);
GO
CREATE TABLE cssd.CycleIndicators (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, CycleId uniqueidentifier NOT NULL,
 Kind varchar(20) NOT NULL CHECK(Kind IN ('Chemical','Biological')), Result varchar(10) NOT NULL CHECK(Result IN ('Pass','Fail')),
 Note nvarchar(500) NOT NULL DEFAULT '', RecordedBy uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), FOREIGN KEY(HospitalId,CycleId) REFERENCES cssd.Cycles(HospitalId,Id)
);
GO
CREATE TABLE cssd.Issues (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, SetId uniqueidentifier NOT NULL, BookingId uniqueidentifier NULL, IssuedTo nvarchar(80) NOT NULL,
 Status varchar(20) NOT NULL CHECK(Status IN ('Issued','Returned')), ReturnCondition varchar(20) NOT NULL DEFAULT '' CHECK(ReturnCondition IN ('','Complete','Incomplete','Damaged')),
 IssuedAtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(), IssuedBy uniqueidentifier NOT NULL, ReturnedAtUtc datetime2(3) NULL,
 Version bigint NOT NULL DEFAULT 1, RowVer rowversion,
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number),
 FOREIGN KEY(HospitalId,SetId) REFERENCES cssd.Sets(HospitalId,Id),
 CHECK((Status='Issued' AND ReturnedAtUtc IS NULL) OR (Status='Returned' AND ReturnedAtUtc IS NOT NULL))
);
GO
CREATE TABLE cssd.Recalls (
 Id uniqueidentifier NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(), HospitalId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL,
 Number varchar(50) NOT NULL, CycleId uniqueidentifier NOT NULL, Reason nvarchar(500) NOT NULL,
 Status varchar(10) NOT NULL CHECK(Status IN ('Open','Closed')), OpenedBy uniqueidentifier NOT NULL, AtUtc datetime2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
 UNIQUE(HospitalId,Id), UNIQUE(HospitalId,Number), FOREIGN KEY(HospitalId,CycleId) REFERENCES cssd.Cycles(HospitalId,Id)
);
GO
INSERT INTO iam.Permissions([Key],Description) VALUES
('insurance.read','Read insurance coverage, preauthorizations and claims'),('insurance.coverage','Register patient insurance coverage'),
('insurance.preauth','Request pre-authorizations'),('insurance.claim','Create, submit and answer insurance claims'),
('insurance.respond','Record payer responses and claim decisions'),('insurance.settle','Record claim settlements'),
('incentive.read','Read incentive rules, batches and payments'),('incentive.rule','Author and retire incentive rules'),
('incentive.calculate','Calculate incentive batches'),('incentive.approve','Approve calculated incentive batches'),('incentive.pay','Record incentive payments'),
('tele.read','Read telemedicine sessions and documents'),('tele.schedule','Schedule telemedicine sessions'),('tele.consult','Run waiting room and consultations'),('tele.upload','Upload telemedicine documents'),
('inventory.read','Read stores, items, batches and assets'),('inventory.indent','Raise inventory indents'),
('inventory.approve','Approve or reject inventory indents'),('inventory.purchase','Issue purchase orders'),('inventory.receive','Receive goods against purchase orders'),
('inventory.transfer','Dispatch and receive store transfers'),('inventory.consume','Record department stock consumption'),
('cssd.read','Read CSSD sets, cycles and issues'),('cssd.process','Register sets, clean, pack and run sterilizer cycles'),
('cssd.release','Release sterilizer cycles'),('cssd.issue','Issue and return instrument sets'),('cssd.recall','Open and close CSSD recalls');
GO
-- Defense in depth: answered preauthorizations, closed claim decision records, paid
-- incentive batches, closed tele sessions and closed sterilizer cycles stay immutable.
CREATE TRIGGER insurance.ProtectAnsweredPreauth ON insurance.Preauthorizations AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Approved','Rejected')) THROW 53010,'Answered preauthorizations are immutable.',1; END;
GO
CREATE TRIGGER insurance.ProtectClosedClaims ON insurance.Claims AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Settled','Rejected')) THROW 53011,'Settled or rejected claims are immutable.',1; END;
GO
CREATE TRIGGER incentive.ProtectPaidBatches ON incentive.Batches AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status='Paid') THROW 53012,'Paid incentive batches are immutable.',1; END;
GO
CREATE TRIGGER tele.AppendOnlyConsent ON tele.ConsentEvents AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 53013,'Consent events are append-only.',1; END;
GO
CREATE TRIGGER tele.ProtectClosedSessions ON tele.Sessions AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Completed','Cancelled')) THROW 53014,'Closed tele sessions are immutable.',1; END;
GO
CREATE TRIGGER inventory.ProtectReceivedOrders ON inventory.PurchaseOrders AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status='Received') THROW 53015,'Received purchase orders are immutable.',1; END;
GO
CREATE TRIGGER cssd.ProtectClosedCycles ON cssd.Cycles AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted WHERE Status IN ('Passed','Failed')) THROW 53016,'Closed sterilizer cycles are immutable.',1; END;
GO
CREATE TRIGGER cssd.AppendOnlyIndicators ON cssd.CycleIndicators AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; THROW 53017,'Cycle indicators are append-only.',1; END;
GO
