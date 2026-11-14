#!/usr/bin/env python3
"""Phase 2 HTTP smoke checks against a loopback synthetic Demo API only."""
import json, os, time, urllib.request, urllib.error, http.cookiejar
BASE = os.environ.get('CAREFLOW_URL', 'http://127.0.0.1:5173') + '/api/v1'
assert 'localhost' in BASE or '127.0.0.1' in BASE, 'Care smoke tests are restricted to loopback demo hosts.'
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
status, patient = call('POST', '/patients', {'firstName': 'Care', 'lastName': f'Smoke{suffix}', 'dateOfBirth': '1990-01-01', 'sex': 'Other'})
check('patient registered for care workflow', status == 200)
_, workspace = call('GET', '/workspace'); care = workspace['workspace']['care']
bed = next(b for b in care['beds'] if b['status'] == 'Available')
status, admission = call('POST', '/ipd/admissions', {'patientId': patient['id'], 'bedId': bed['id'], 'doctorId': workspace['workspace']['doctors'][0]['id'], 'reason': 'Synthetic care smoke admission', 'admissionType': 'Emergency', 'diagnosisOnAdmission': 'Synthetic dehydration', 'attendantName': 'Smoke Attendant', 'attendantPhone': '+91 9000000099', 'payerType': 'Self', 'expectedDischargeDate': '2027-01-15'})
check('admission occupies bed', status == 200 and admission['status'] == 'Active')
check('admission clinical and payer details are captured', admission['admissionType'] == 'Emergency' and admission['diagnosisOnAdmission'] == 'Synthetic dehydration' and admission['attendantName'] == 'Smoke Attendant')
check('occupied bed cannot be reused', call('POST', '/ipd/admissions', {'patientId': workspace['workspace']['patients'][0]['id'], 'bedId': bed['id'], 'doctorId': workspace['workspace']['doctors'][0]['id'], 'reason': 'Duplicate bed attempt'})[0] == 409)
status, note = call('POST', '/nursing/notes', {'admissionId': admission['id'], 'kind': 'Assessment', 'text': 'Synthetic nursing escalation', 'vitals': {'temperatureC': None, 'pulse': None, 'systolic': None, 'diastolic': None, 'weightKg': None, 'spo2': 88}, 'intakeMl': None, 'outputMl': None, 'painScore': 8, 'fallRisk': 'High'}, role='Nurse')
check('nursing note creates escalation', status == 200)
_, workspace = call('GET', '/workspace'); alerts = workspace['workspace']['care']['criticalAlerts']
check('nursing alert is open', any(a['entityId'] == note['id'] and a['status'] == 'Open' for a in alerts))
medicine = workspace['workspace']['medicines'][0]
status, batch = call('POST', '/pharmacy/stock', {'medicineId': medicine['id'], 'batchNumber': f'SMOKE-{suffix}', 'expiryDate': '2027-11-13', 'quantity': 10, 'unitCost': 1, 'supplier': 'Synthetic supplier', 'reference': f'GRN-{suffix}'}, role='Pharmacist')
check('pharmacist receives stock', status == 200)
check('nurse cannot receive stock', call('POST', '/pharmacy/stock', {'medicineId': medicine['id'], 'batchNumber': f'NO-{suffix}', 'expiryDate': '2027-11-13', 'quantity': 1, 'unitCost': 1, 'supplier': 'S', 'reference': 'R'}, role='Nurse')[0] == 403)
status, order = call('POST', '/lab/orders', {'patientId': patient['id'], 'testId': next(t['id'] for t in workspace['workspace']['care']['labTests'] if t['code'] == 'HB'), 'encounterId': None, 'admissionId': None, 'priority': 'STAT', 'clinicalNote': 'Synthetic critical workflow'}, role='Doctor')
check('doctor orders laboratory test', status == 200)
status, order = call('POST', f"/lab/orders/{order['id']}/collect", {'specimenType': 'Blood', 'volumeMl': 2, 'version': order['version']}, role='Lab Technician')
check('specimen collection allocates barcode/accession', status == 200 and order['barcode'] and order['accession'])
status, order = call('POST', f"/lab/orders/{order['id']}/process", {'version': order['version']})
check('specimen enters processing', status == 200)
status, result = call('POST', f"/lab/orders/{order['id']}/results", {'value': '6.5', 'unit': 'g/dL', 'referenceLow': None, 'referenceHigh': None, 'version': order['version']})
check('critical result is flagged', status == 200 and result['flag'] == 'Critical low')
order['version'] += 1
check('same technician cannot validate own result', call('POST', f"/lab/orders/{order['id']}/validate", {'version': order['version']})[0] == 409)
status, validated = call('POST', f"/lab/orders/{order['id']}/validate", {'version': order['version']}, login={'userName': 'labtech2', 'password': 'Preview!2026'})
check('second technician validates result', status == 200 and validated['validatedAt'])
order['version'] += 1
status, authorized = call('POST', f"/lab/orders/{order['id']}/authorize", {'version': order['version']}, role='Pathologist')
check('pathologist authorizes report with content hash', status == 200 and len(authorized['contentHash']) == 64)
_, workspace = call('GET', '/workspace'); alert = next(a for a in workspace['workspace']['care']['criticalAlerts'] if a['entityId'] == order['id'] and a['kind'] == 'Laboratory')
status, ack = call('POST', f"/alerts/{alert['id']}/acknowledge", {'note': 'Synthetic clinician acknowledgement'}, role='Doctor')
check('critical alert acknowledged with note', status == 200 and ack['status'] == 'Acknowledged')
check('acknowledged alert is immutable', call('POST', f"/alerts/{alert['id']}/acknowledge", {'note': 'Duplicate acknowledgement'})[0] == 409)
_, workspace = call('GET', '/workspace'); nurse2 = next((n for n in workspace['workspace']['care']['nurses'] if n['name'] == 'Amrita Nair'), None)
check('handover directory lists another nurse', nurse2 is not None)
status, handover = call('POST', '/nursing/handovers', {'toNurseId': nurse2['id'], 'summary': 'Synthetic shift handover for smoke verification.', 'patientIds': [patient['id']]}, role='Nurse')
check('nurse creates named shift handover', status == 200)
status, ack = call('POST', f"/nursing/handovers/{handover['id']}/acknowledge", {'version': handover['version'], 'reason': 'Synthetic handover received'}, login={'userName': 'nurse2', 'password': 'Preview!2026'})
check('named incoming nurse acknowledges handover', status == 200 and ack['status'] == 'Acknowledged')
status, discharged = call('POST', f"/ipd/admissions/{admission['id']}/discharge", {'version': admission['version'], 'summary': 'Synthetic discharge summary for smoke verification.', 'diagnosis': 'Synthetic diagnosis', 'instructions': 'Synthetic follow-up instructions'}, role='Doctor')
check('doctor discharges with immutable summary', status == 200 and discharged['status'] == 'Discharged')
print(f'\n{passed} Phase 2 HTTP smoke checks passed.')
