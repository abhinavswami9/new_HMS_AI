-- OPTIONAL development configuration for Phase 2. Run AFTER migration 0002 and the
-- Phase 1 example on a disposable database only. Never use as production master data.
-- Beds, lab tests and reference/critical thresholds require hospital governance.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @h uniqueidentifier='10000000-0000-0000-0000-000000000001', @b uniqueidentifier='20000000-0000-0000-0000-000000000001';
IF EXISTS(SELECT 1 FROM ipd.Beds WHERE HospitalId=@h AND BranchId=@b) THROW 52020,'Phase 2 example configuration is install-once only.',1;
UPDATE org.ModuleInstallations SET Enabled=1,UpdatedAtUtc=SYSUTCDATETIME() WHERE HospitalId=@h AND BranchId=@b AND ModuleKey IN ('ipd','nursing','pharmacy','laboratory');
INSERT INTO ipd.Beds(HospitalId,BranchId,Ward,Number,Kind,Status) VALUES
(@h,@b,'General ward','G-01','General','Available'),(@h,@b,'General ward','G-02','General','Available'),
(@h,@b,'General ward','G-03','General','Available'),(@h,@b,'General ward','G-04','General','Available'),
(@h,@b,'ICU','ICU-01','ICU','Available'),(@h,@b,'ICU','ICU-02','ICU','Available'),
(@h,@b,'Private','P-01','Private','Available'),(@h,@b,'Private','P-02','Private','Maintenance');
INSERT INTO lab.Tests(HospitalId,Code,Name,SpecimenType,Unit,ReferenceLow,ReferenceHigh,CriticalLow,CriticalHigh) VALUES
(@h,'HB','Hemoglobin','Blood','g/dL',12,16,7,20),
(@h,'GLU','Blood glucose','Blood','mg/dL',70,140,50,400),
(@h,'CREAT','Creatinine','Blood','mg/dL',0.6,1.3,NULL,5);
-- The migration seeds nursing.escalation with ApprovedBy NULL. Approve it through the
-- hospital's governed policy process before recording nursing observations, e.g. after
-- an authorized policy reviewer exists:
-- UPDATE org.Policies SET ApprovedBy=@reviewerUserId,Reason='Approved clinical escalation thresholds'
-- WHERE HospitalId=@h AND BranchId=@b AND [Key]='nursing.escalation' AND Version=1;
COMMIT TRANSACTION;
