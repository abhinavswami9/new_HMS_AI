#!/usr/bin/env python3
"""Phase 3 (RIS, emergency, theatre, blood bank) HTTP smoke checks against a loopback synthetic Demo API only."""
import json, os, time, urllib.request, urllib.error, http.cookiejar
BASE = os.environ.get('CAREFLOW_URL', 'http://127.0.0.1:5173') + '/api/v1'
assert 'localhost' in BASE or '127.0.0.1' in BASE, 'Phase 3 smoke tests are restricted to loopback demo hosts.'
jar = http.cookiejar.CookieJar(); client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar)); passed = 0

def call(method, path, body=None, role=None, login=None):
    if role: call('POST', '/auth/demo', {'role': role})
    if login: call('POST', '/auth/login', login)
    headers = {'X-Requested-With': 'Careflow', 'Origin': BASE.split('/api')[0]}
    data = None
    if body is not None: headers['Content-Type'] = 'application/json'; data = json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        with client.open(req, timeout=15) as r: raw = r.read(); return r.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as e: raw = e.read(); return e.code, json.loads(raw) if raw else None

def check(name, condition):
    global passed
    assert condition, name
    passed += 1; print('PASS', name)

call('POST', '/auth/demo', {})
suffix = str(time.time_ns())
status, patient = call('POST', '/patients', {'firstName': 'P3', 'lastName': f'Smoke{suffix}', 'dateOfBirth': '1990-01-01', 'sex': 'Other', 'bloodGroup': 'B+', 'phone': f'+91 9{time.time_ns() % 1000000000:09d}', 'email': f'p3smoke{suffix}@example.com', 'address': 'Synthetic', 'emergencyContact': 'Synthetic', 'duplicateReviewReason': ''})
check('patient registered for phase 3 workflows', status == 200)
_, workspace = call('GET', '/workspace'); care = workspace['workspace']['care']
check('workspace exposes phase 3 worklists', all(k in care for k in ['radiologyOrders', 'radiologyReports', 'emergencyRegistrations', 'otBookings', 'bloodUnits', 'bloodIssues']))
check('demo radiology orders are seeded', len(care['radiologyOrders']) >= 3)

# RIS: order -> schedule -> perform -> report -> authorize with separation
status, order = call('POST', '/radiology/orders', {'patientId': patient['id'], 'encounterId': None, 'admissionId': None, 'procedure': 'CT Brain plain', 'modality': 'CT', 'bodyPart': 'Brain', 'laterality': 'Not applicable', 'priority': 'Urgent', 'clinicalNote': 'Synthetic acute onset deficit'}, role='Doctor')
check('doctor orders radiology study', status == 200 and order['number'].startswith('RIS-'))
check('paired site without laterality is rejected', call('POST', '/radiology/orders', {'patientId': patient['id'], 'encounterId': None, 'admissionId': None, 'procedure': 'X-ray knee', 'modality': 'X-ray', 'bodyPart': 'Knee', 'laterality': 'Not applicable', 'priority': 'Routine', 'clinicalNote': 'Synthetic knee pain review'}, role='Doctor')[0] == 422)
check('nurse cannot order radiology', call('POST', '/radiology/orders', {'patientId': patient['id'], 'encounterId': None, 'admissionId': None, 'procedure': 'X-ray chest', 'modality': 'X-ray', 'bodyPart': 'Chest', 'laterality': 'Not applicable', 'priority': 'Routine', 'clinicalNote': 'Synthetic role probe'}, role='Nurse')[0] == 403)
status, order = call('POST', f"/radiology/orders/{order['id']}/schedule", {'at': '2026-11-14T09:00:00+00:00', 'version': order['version']}, login={'userName': 'radiologist', 'password': 'Preview!2026'})
check('radiologist schedules study with accession', status == 200 and order['accession'].startswith('ACC-R-'))
status, order = call('POST', f"/radiology/orders/{order['id']}/perform", {'studyUid': '1.2.840.113619.2.55.3.604688119.868.1762000002.789', 'version': order['version']})
check('study performed with DICOM UID', status == 200 and order['studyUid'].startswith('1.'))
status, report = call('POST', f"/radiology/orders/{order['id']}/report", {'findings': 'Synthetic hyperdense focus in the right basal ganglia.', 'impression': 'Synthetic acute intracerebral hemorrhage. Critical result communicated.', 'critical': True, 'version': order['version']})
check('critical radiology report recorded', status == 200 and report['status'] == 'Reported')
_, workspace = call('GET', '/workspace'); alerts = workspace['workspace']['care']['criticalAlerts']
check('critical radiology alert is open', any(a['entityId'] == order['id'] and a['kind'] == 'Radiology' and a['status'] == 'Open' for a in alerts))
order['version'] += 1
check('reporter cannot authorize own report', call('POST', f"/radiology/orders/{order['id']}/authorize", {'reason': '', 'version': order['version']})[0] == 403)
status, authorized = call('POST', f"/radiology/orders/{order['id']}/authorize", {'reason': '', 'version': order['version']}, login={'userName': 'radiologist2', 'password': 'Preview!2026'})
check('second radiologist authorizes with hash', status == 200 and authorized['status'] == 'Authorized' and len(authorized['contentHash']) == 64)
check('authorized study cannot be cancelled', call('POST', f"/radiology/orders/{order['id']}/cancel", {'reason': 'Synthetic', 'version': order['version'] + 1}, role='Doctor')[0] == 409)
check('PACS viewer launch fails closed', call('GET', f"/radiology/studies/{order['id']}/viewer-launch")[0] == 501)

# Emergency: register -> triage -> observe -> dispose
status, er = call('POST', '/emergency/registrations', {'patientId': patient['id'], 'patientName': f'P3 Smoke{suffix}', 'sex': 'Other', 'dateOfBirth': None, 'arrivalMode': 'Ambulance', 'referringFacility': 'P3 Referral Clinic', 'broughtBy': 'Family escort', 'medicoLegalCase': True}, role='Doctor')
check('emergency episode registered', status == 200 and er['number'].startswith('ER-'))
check('emergency arrival details are captured', er['referringFacility'] == 'P3 Referral Clinic' and er['broughtBy'] == 'Family escort' and er['medicoLegalCase'] is True)
check('identity mismatch is rejected', call('POST', '/emergency/registrations', {'patientId': patient['id'], 'patientName': 'Wrong Name', 'sex': 'Other', 'dateOfBirth': None, 'arrivalMode': 'Walk-in'}, role='Doctor')[0] == 409)
status, triage = call('POST', f"/emergency/registrations/{er['id']}/triage", {'category': 'Red', 'complaint': 'Synthetic crushing chest pain with diaphoresis', 'vitals': {'temperatureC': 36.8, 'pulse': 110, 'systolic': 140, 'diastolic': 90, 'weightKg': None, 'spo2': 94}}, role='Nurse')
check('triage assessment recorded', status == 200 and triage['category'] == 'Red')
status, observation = call('POST', f"/emergency/registrations/{er['id']}/observations", {'note': 'Synthetic observation after initial analgesia.', 'vitals': {'temperatureC': None, 'pulse': 96, 'systolic': 132, 'diastolic': 84, 'weightKg': None, 'spo2': 96}}, role='Doctor')
check('emergency observation recorded', status == 200)
status, disposed = call('POST', f"/emergency/registrations/{er['id']}/disposition", {'kind': 'Discharge', 'summary': 'Synthetic discharge home with cardiology follow-up advice.', 'bedId': None, 'doctorId': None, 'receivingFacility': None}, role='Doctor')
check('episode dispositioned and closed', status == 200 and disposed['status'] == 'Dispositioned')
check('closed episode refuses further triage', call('POST', f"/emergency/registrations/{er['id']}/triage", {'category': 'Green', 'complaint': 'Synthetic post-disposition probe', 'vitals': {'temperatureC': None, 'pulse': None, 'systolic': None, 'diastolic': None, 'weightKg': None, 'spo2': None}}, role='Nurse')[0] == 409)

# Theatre: request -> schedule (conflict) -> checklist -> time-in/out -> complete
_, workspace = call('GET', '/workspace'); active = next(a for a in workspace['workspace']['care']['admissions'] if a['status'] == 'Active')
status, booking = call('POST', '/ot/bookings', {'patientId': active['patientId'], 'admissionId': active['id'], 'procedure': 'Synthetic forearm incision and drainage', 'laterality': 'Left', 'site': 'Left forearm', 'surgeonId': workspace['workspace']['doctors'][0]['id'], 'anaesthesia': 'Local', 'priority': 'Elective'}, role='Doctor')
check('theatre booking requested', status == 200 and booking['number'].startswith('OT-'))
status, booking = call('POST', f"/ot/bookings/{booking['id']}/schedule", {'theatre': f'SMOKE-OT-{suffix}', 'start': '2026-11-15T09:00:00+00:00', 'durationMinutes': 60, 'version': booking['version']}, role='Hospital Admin')
check('booking scheduled into theatre', status == 200 and booking['status'] == 'Scheduled')
status, booking2 = call('POST', '/ot/bookings', {'patientId': active['patientId'], 'admissionId': active['id'], 'procedure': 'Synthetic overlapping procedure', 'laterality': 'Right', 'site': 'Right forearm', 'surgeonId': workspace['workspace']['doctors'][0]['id'], 'anaesthesia': 'Local', 'priority': 'Elective'}, role='Doctor')
check('overlapping theatre slot rejected', call('POST', f"/ot/bookings/{booking2['id']}/schedule", {'theatre': f'SMOKE-OT-{suffix}', 'start': '2026-11-15T09:30:00+00:00', 'durationMinutes': 60, 'version': booking2['version']}, role='Hospital Admin')[0] == 409)
check('time-in before checklist is rejected', call('POST', f"/ot/bookings/{booking['id']}/time-in", {'reason': '', 'version': booking['version']}, role='Doctor')[0] == 409)
status, checklist = call('POST', f"/ot/bookings/{booking['id']}/checklist", {'consent': True, 'anaesthesiaAssessment': True, 'fastingConfirmed': True, 'siteMarked': True, 'safetyChecklist': True, 'notes': 'Synthetic surgical safety checklist completed.'}, role='Doctor')
check('surgical safety checklist recorded', status == 200)
status, booking = call('POST', f"/ot/bookings/{booking['id']}/time-in", {'reason': '', 'version': booking['version']}, role='Doctor')
check('theatre time-in recorded', status == 200 and booking['timeIn'])
status, booking = call('POST', f"/ot/bookings/{booking['id']}/time-out", {'reason': '', 'version': booking['version']}, role='Doctor')
check('theatre time-out recorded', status == 200 and booking['timeOut'])
status, booking = call('POST', f"/ot/bookings/{booking['id']}/complete", {'operationNote': 'Synthetic procedure note: abscess drained, hemostasis achieved.', 'version': booking['version']}, role='Doctor')
check('operation completed with note', status == 200 and booking['status'] == 'Completed')
check('completed surgery cannot be cancelled', call('POST', f"/ot/bookings/{booking['id']}/cancel", {'reason': 'Synthetic', 'version': booking['version'] + 1}, role='Hospital Admin')[0] == 409)

# Blood bank: receive -> screen -> crossmatch -> issue -> transfuse -> immutable closure
status, unit = call('POST', '/blood/units', {'unitNumber': f'SMK-{suffix}', 'bloodGroup': 'O-', 'component': 'PRBC', 'expiryDate': '2026-12-15', 'donorName': 'Synthetic voluntary donor'}, role='Blood Bank Technician')
check('blood unit received into quarantine', status == 200 and unit['status'] == 'Quarantined')
check('duplicate unit number rejected', call('POST', '/blood/units', {'unitNumber': f'SMK-{suffix}', 'bloodGroup': 'O-', 'component': 'PRBC', 'expiryDate': '2026-12-15', 'donorName': 'Synthetic voluntary donor'}, role='Blood Bank Technician')[0] == 409)
status, unit = call('POST', f"/blood/units/{unit['id']}/screen", {'hbsag': False, 'hiv': False, 'hcv': False, 'vdrl': False, 'notes': 'Synthetic clean screening'}, role='Blood Bank Technician')
check('clean screening releases unit', status == 200 and unit['status'] == 'Available')
status, xm = call('POST', '/blood/crossmatches', {'unitId': unit['id'], 'patientId': patient['id']}, role='Blood Bank Technician')
check('crossmatch compatible and reserves unit', status == 200 and xm['compatibility'] == 'Compatible')
status, issue = call('POST', '/blood/issues', {'unitId': unit['id'], 'patientId': patient['id'], 'admissionId': None, 'notes': 'Synthetic transfusion request'}, role='Blood Bank Technician')
check('blood unit issued', status == 200 and issue['number'].startswith('BLD-'))
status, issue = call('POST', f"/blood/issues/{issue['id']}/transfuse", {'reaction': 'Rigors and chills', 'notes': 'Synthetic transfusion reaction', 'version': issue['version']}, role='Doctor')
check('transfusion reaction recorded', status == 200 and issue['status'] == 'Transfused')
_, workspace = call('GET', '/workspace'); alerts = workspace['workspace']['care']['criticalAlerts']
check('blood reaction alert is open', any(a['entityId'] == issue['id'] and a['kind'] == 'Blood' and a['status'] == 'Open' for a in alerts))
check('transfused issue is immutable', call('POST', f"/blood/issues/{issue['id']}/transfuse", {'reaction': 'Nil', 'notes': 'Duplicate attempt', 'version': issue['version'] + 1}, role='Doctor')[0] == 409)

print(f'\n{passed} phase 3 smoke checks passed against {BASE.rsplit("/api", 1)[0]}')
