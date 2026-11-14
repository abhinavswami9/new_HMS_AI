-- OPTIONAL development configuration, not an approved production formulary/price list.
-- Run AFTER migration 0001 and BEFORE provision-admin. Never execute against a live hospital
-- without replacing names, prices, medicines and identifiers with governed master data.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @h uniqueidentifier='10000000-0000-0000-0000-000000000001', @b uniqueidentifier='20000000-0000-0000-0000-000000000001';
IF EXISTS(SELECT 1 FROM org.Hospitals WHERE Id=@h) THROW 51020,'Example configuration is install-once only.',1;
INSERT INTO org.Hospitals(Id,Code,Name) VALUES(@h,'NOVA-DEV','Nova Care Hospital');
INSERT INTO org.Branches(Id,HospitalId,Code,Name) VALUES(@b,@h,'MAIN','Main branch · New Delhi');
INSERT INTO org.ModuleInstallations(HospitalId,BranchId,ModuleKey,Enabled) SELECT @h,@b,[Key],CASE WHEN Phase=1 THEN 1 ELSE 0 END FROM org.ModuleCatalog;
INSERT INTO org.Departments(HospitalId,BranchId,Name) VALUES(@h,@b,'Cardiology'),(@h,@b,'General Medicine'),(@h,@b,'Orthopaedics'),(@h,@b,'Pediatrics'),(@h,@b,'Dermatology');
INSERT INTO catalog.Practitioners(Id,HospitalId,Name,Department,Initials,Color) VALUES
('40000000-0000-0000-0000-000000000001',@h,'Dr. Anjali Sharma','Cardiology','AS','#568f83'),
('40000000-0000-0000-0000-000000000002',@h,'Dr. Rahul Verma','General Medicine','RV','#668eaf'),
('40000000-0000-0000-0000-000000000003',@h,'Dr. Priya Menon','Orthopaedics','PM','#9b85af'),
('40000000-0000-0000-0000-000000000004',@h,'Dr. Arjun Desai','Pediatrics','AD','#c1a064'),
('40000000-0000-0000-0000-000000000005',@h,'Dr. Neha Kapoor','Dermatology','NK','#b58491');
INSERT INTO catalog.Services(Id,HospitalId,Code,Name,Department) VALUES
('60000000-0000-0000-0000-000000000001',@h,'OPD-GEN','General consultation','General Medicine'),
('60000000-0000-0000-0000-000000000002',@h,'OPD-SPEC','Specialist consultation','Cardiology'),
('60000000-0000-0000-0000-000000000003',@h,'LAB-CBC','Complete blood count (CBC)','Laboratory'),
('60000000-0000-0000-0000-000000000004',@h,'RAD-XR1','X-ray · single view','Radiology'),
('60000000-0000-0000-0000-000000000005',@h,'CARD-ECG','Electrocardiogram (ECG)','Cardiology'),
('60000000-0000-0000-0000-000000000006',@h,'PROC-DRESS','Dressing & wound care','Procedures'),
('60000000-0000-0000-0000-000000000007',@h,'OPD-FOLLOW','Follow-up consultation','General Medicine');
INSERT INTO catalog.Prices(HospitalId,BranchId,ServiceId,Amount,EffectiveFromUtc)
SELECT @h,@b,Id,CASE Code WHEN 'OPD-GEN' THEN 600 WHEN 'OPD-SPEC' THEN 1000 WHEN 'LAB-CBC' THEN 350 WHEN 'RAD-XR1' THEN 900 WHEN 'CARD-ECG' THEN 500 WHEN 'PROC-DRESS' THEN 250 ELSE 400 END,SYSUTCDATETIME() FROM catalog.Services WHERE HospitalId=@h;
INSERT INTO catalog.Medicines(Id,HospitalId,GenericName,BrandName,Strength,Form) VALUES
('70000000-0000-0000-0000-000000000001',@h,'Paracetamol','Crocin','500 mg','Tablet'),
('70000000-0000-0000-0000-000000000002',@h,'Amoxicillin','Mox','500 mg','Capsule'),
('70000000-0000-0000-0000-000000000003',@h,'Cetirizine','Cetzine','10 mg','Tablet'),
('70000000-0000-0000-0000-000000000004',@h,'Metformin','Glycomet','500 mg','Tablet'),
('70000000-0000-0000-0000-000000000005',@h,'Amlodipine','Amlong','5 mg','Tablet'),
('70000000-0000-0000-0000-000000000006',@h,'Pantoprazole','Pantocid','40 mg','Tablet'),
('70000000-0000-0000-0000-000000000007',@h,'Azithromycin','Azithral','500 mg','Tablet'),
('70000000-0000-0000-0000-000000000008',@h,'Ibuprofen','Brufen','400 mg','Tablet');
COMMIT TRANSACTION;
