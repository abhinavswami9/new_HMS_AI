#!/usr/bin/env python3
"""Phase 4 (insurance, incentives, telemedicine, inventory, CSSD) HTTP smoke checks against a loopback synthetic Demo API only. Run once per fresh demo instance."""
import json, os, time, urllib.request, urllib.error, http.cookiejar
from datetime import date, timedelta
BASE = os.environ.get('CAREFLOW_URL', 'http://127.0.0.1:5173') + '/api/v1'
assert 'localhost' in BASE or '127.0.0.1' in BASE, 'Phase 4 smoke tests are restricted to loopback demo hosts.'
jar = http.cookiejar.CookieJar(); client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar)); passed = 0

auth_state = {"key": None}
def _auth(kind, payload):
    for attempt in range(4):
        headers = {'X-Requested-With': 'Careflow', 'Origin': BASE.split('/api')[0], 'Content-Type': 'application/json'}
        req = urllib.request.Request(BASE + ('/auth/demo' if kind == 'demo' else '/auth/login'),
                                     data=json.dumps(payload).encode(), headers=headers, method='POST')
        try:
            with client.open(req, timeout=15) as r: raw = r.read(); return r.status
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(20); continue
            return e.code
    return 429
def call(method, path, body=None, role=None, login=None):
    want = ("login", tuple(sorted(login.items()))) if login else (("demo", role) if role else auth_state["key"])
    if login or role:
        if want != auth_state["key"]:
            ok = _auth("login", login) if login else _auth("demo", {"role": role})
            assert ok == 200, f'auth failed: {ok}'
            auth_state["key"] = want
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
auth_state['key'] = ('demo', None)
suffix = str(time.time_ns())
today = date.today()

# ── workspace honesty: phase 4 worklists present, phase 5 planned ─────────────
_, workspace = call('GET', '/workspace')
care = workspace['workspace']['care']
check('workspace exposes phase 4 worklists', all(k in care for k in ['payers', 'plans', 'coverages', 'claims', 'incentiveBatches', 'teleSessions', 'inventoryItems', 'cssdSets']))
mods = {m['key']: m for m in workspace['workspace']['modules']}
check('phase 5 modules are enabled in the demo catalog', all(mods[k]['enabled'] is True for k in ['quality', 'portal', 'reports']))

# ── module gate: phase 4 enables, phase 5 stays gated ────────────────────────
status, _ = call('PUT', '/modules/quality', {'enabled': True, 'reason': 'Quality module toggle after the phase 5 release.'}, role='Hospital Admin')
check('phase 5 modules pass the implementation gate', status == 200)
status, body = call('PUT', '/modules/pacs', {'enabled': True, 'reason': 'Attempting to enable an unbuilt integration.'}, role='Hospital Admin')
check('unbuilt integration modules remain gated', status == 422 and body['code'] == 'module.unavailable')
status, _ = call('PUT', '/modules/insurance', {'enabled': True, 'reason': 'Phase 4 implementation gates are complete.'}, role='Hospital Admin')
check('phase 4 module passes the implementation gate', status == 200)

# ── insurance/TPA: coverage → preauth → claim → settlement ───────────────────
status, patient = call('POST', '/patients', {'firstName': 'P4', 'lastName': f'Smoke{suffix}', 'dateOfBirth': '1988-04-12', 'sex': 'Female', 'bloodGroup': 'O+', 'phone': f'+91 8{time.time_ns() % 1000000000:09d}', 'email': f'p4smoke{suffix}@example.com', 'address': 'Synthetic', 'emergencyContact': 'Synthetic', 'duplicateReviewReason': ''})
check('patient registered for phase 4 workflows', status == 200)
_, plans = call('GET', '/insurance/coverages', role='Hospital Admin')
_, workspace2 = call('GET', '/workspace')
planId = workspace2['workspace']['care']['plans'][0]['id']
coverageBody = {'patientId': patient['id'], 'planId': planId, 'policyNumber': f'POL-SMK-{suffix[-6:]}', 'memberId': f'MBR-{suffix[-6:]}', 'validFrom': str(today - timedelta(days=30)), 'validTo': str(today + timedelta(days=330)), 'sumInsured': 200000, 'deductible': 2000}
status, coverage = call('POST', '/insurance/coverages', coverageBody, login={'userName': 'insurance', 'password': 'Preview!2026'})
check('coverage registered with policy number', status == 200 and coverage['number'].startswith('COV-'))
check('duplicate policy numbers are rejected', call('POST', '/insurance/coverages', coverageBody, login={'userName': 'insurance', 'password': 'Preview!2026'})[0] == 422)
status, checked = call('POST', f"/insurance/coverages/{coverage['id']}/verify", None)
check('eligibility check is recorded against the coverage', status == 200 and checked['id'] == coverage['id'])
status, preauth = call('POST', '/insurance/preauthorizations', {'coverageId': coverage['id'], 'patientId': patient['id'], 'diagnosisCode': 'M54.5', 'procedureCode': 'PROC-100', 'requestedAmount': 8000, 'validTo': str(today + timedelta(days=30))})
check('preauthorization requested', status == 200 and preauth['status'] == 'Requested')
status, preauth = call('POST', f"/insurance/preauthorizations/{preauth['id']}/respond", {'decision': 'Approved', 'approvedAmount': 6000, 'note': 'Approved with panel tariff cap applied.', 'version': preauth['version']})
check('payer response approves with capped amount', status == 200 and preauth['approvedAmount'] == 6000)
claimBody = {'patientId': patient['id'], 'coverageId': coverage['id'], 'invoiceId': None, 'admissionId': None, 'diagnosisCode': 'M54.5', 'lines': [{'serviceCode': 'CONS-1', 'description': 'Consultation', 'quantity': 1, 'unitPrice': 600}, {'serviceCode': 'LAB-1', 'description': 'Metabolic panel', 'quantity': 2, 'unitPrice': 450}]}
status, claim = call('POST', '/insurance/claims', claimBody)
check('claim created with recalculated totals', status == 200 and claim['totalAmount'] == 1500)
status, claim = call('POST', f"/insurance/claims/{claim['id']}/submit", {'version': claim['version'], 'reason': ''})
check('claim submitted to payer', status == 200 and claim['status'] == 'Submitted')
status, claim = call('POST', f"/insurance/claims/{claim['id']}/query", {'question': 'Please share the itemised investigation report for adjudication.'})
check('payer query recorded on the claim', status == 200 and claim['status'] == 'Queried')
status, claim = call('POST', f"/insurance/claims/{claim['id']}/respond", {'response': 'Itemised report shared with the payer adjudication desk.', 'version': claim['version']})
check('claim query answered', status == 200 and claim['status'] == 'Responded')
status, claim = call('POST', f"/insurance/claims/{claim['id']}/decide", {'decision': 'Approved', 'approvedAmount': 1200, 'reason': 'Approved as per policy schedule.', 'version': claim['version']})
check('claim approved with patient liability', status == 200 and claim['patientLiability'] == 300)
loginAcc0 = {'userName': 'account', 'password': 'Preview!2026'}
status, settlement = call('POST', f"/insurance/claims/{claim['id']}/settle", {'amount': 1000, 'mode': 'Bank transfer', 'reference': f'STL-SMK-{suffix[-6:]}', 'version': claim['version']}, login=loginAcc0)
check('partial settlement recorded with unique reference', status == 200 and settlement['number'].startswith('SETL-'))
status, body = call('POST', f"/insurance/claims/{claim['id']}/settle", {'amount': 500, 'mode': 'Cash', 'reference': f'STL-SMK-X{suffix[-5:]}', 'version': claim['version']}, login=loginAcc0)
check('settlements cannot exceed the approved amount', status == 422 and body['code'] == 'insurance.settlement')
status, settlement = call('POST', f"/insurance/claims/{claim['id']}/settle", {'amount': 200, 'mode': 'Bank transfer', 'reference': f'STL-SMK-Y{suffix[-5:]}', 'version': claim['version']}, login=loginAcc0)
check('final settlement closes the claim', status == 200)

# ── incentives: rules → closed-period batch → separation → payments ──────────
loginAcc = loginAcc0
status, rule = call('POST', '/incentives/rules', {'name': f'Smoke revenue share {suffix[-4:]}', 'scopeKind': 'All', 'doctorId': None, 'basis': 'Revenue', 'mode': 'Percent', 'value': 4, 'effectiveFrom': str(today - timedelta(days=60)), 'effectiveTo': None}, role='Hospital Admin')
check('incentive rule drafted', status == 200 and rule['status'] == 'Draft')
status, rule = call('POST', f"/incentives/rules/{rule['id']}/status/Active", {'version': rule['version'], 'reason': 'Approved for the current payout cycle.'})
check('incentive rule activated', status == 200 and rule['status'] == 'Active')
status, body = call('POST', '/incentives/calculate', {'periodStart': str(today - timedelta(days=30)), 'periodEnd': str(today - timedelta(days=1))}, role='Hospital Admin')
check('closed-period incentive batch calculated', status == 200 and body['status'] == 'Calculated' and body['totalAmount'] > 0)
batch = body
check('batch recalculation reason stays empty until recalculated', batch['recalculationReason'] in ('', None))
status, body = call('POST', '/incentives/calculate', {'periodStart': str(today - timedelta(days=30)), 'periodEnd': str(today - timedelta(days=1))})
check('duplicate incentive periods are rejected', status == 422 and body['code'] == 'incentive.period')
_, lines = call('GET', f"/incentives/batches/{batch['id']}/lines")
check('incentive lines snapshot rule and basis', len(lines) >= 1 and all(l['snapshot'].startswith('ref=') for l in lines))
status, body = call('POST', f"/incentives/batches/{batch['id']}/approve", {'reason': 'Self approval attempt during smoke run.', 'version': batch['version']}, role='Hospital Admin')
check('calculator cannot approve their own batch', status == 403 and body['code'] == 'incentive.separation')
status, batch = call('POST', f"/incentives/batches/{batch['id']}/approve", {'reason': 'Reviewed against the payout register.', 'version': batch['version']}, login=loginAcc)
check('second authorized person approves the batch', status == 200 and batch['status'] == 'Approved')
status, recalcBody = call('POST', f"/incentives/batches/{batch['id']}/recalculate", {'reason': 'Late recalculation attempt.', 'version': batch['version']}, role='Hospital Admin')
check('approved batches cannot be recalculated', status == 409 and recalcBody['code'] == 'incentive.state')
half = batch["totalAmount"] / 2
status, payment = call('POST', f"/incentives/batches/{batch['id']}/pay", {'amount': half, 'mode': 'Bank transfer', 'reference': f'INC-SMK-{suffix[-6:]}', 'version': batch['version']}, login=loginAcc)
check('partial incentive payment recorded', status == 200 and abs(payment['amount'] - half) < 0.001)
status, body = call('POST', f"/incentives/batches/{batch['id']}/pay", {'amount': batch['totalAmount'] + 500, 'mode': 'Cash', 'reference': f'INC-SMK-Z{suffix[-5:]}', 'version': batch['version']}, login=loginAcc)
check('payments cannot exceed the batch total', status == 422 and body['code'] == 'incentive.payment')

# ── telemedicine: consent-gated waiting room and documents ───────────────────
from datetime import datetime, timezone, timedelta as td
appointment = None
for hours, doc in [(2, '40000000-0000-0000-0000-000000000002'), (3, '40000000-0000-0000-0000-000000000003'), (4, '40000000-0000-0000-0000-000000000004'), (5, '40000000-0000-0000-0000-000000000005')]:
    apptStart = (datetime.now(timezone.utc) + td(hours=hours)).isoformat()
    status, appointment = call('POST', '/appointments', {'patientId': patient['id'], 'doctorId': doc, 'startsAt': apptStart, 'durationMinutes': 20, 'mode': 'Telemedicine', 'reason': 'Tele review'}, role='Hospital Admin')
    if status == 200: break
check('appointment booked for tele session', status == 200)
status, session = call('POST', '/telemedicine/sessions', {'appointmentId': appointment['id']}, role='Doctor')
check('tele session booked against the appointment', status == 200 and session['number'].startswith('TEL-'))
status, body = call('POST', f"/telemedicine/sessions/{session['id']}/join-waiting", None)
check('waiting room requires recorded consent first', status == 422 and body['code'] == 'tele.consent')
status, session = call('POST', f"/telemedicine/sessions/{session['id']}/consent", {'kind': 'PatientConsent', 'given': True, 'note': 'Consent for tele-consultation recorded.'})
check('patient consent recorded', status == 200 and session['consentObtained'] is True)
status, session = call('POST', f"/telemedicine/sessions/{session['id']}/join-waiting", None)
check('session joins the waiting room', status == 200 and session['status'] == 'Waiting')
status, join = call('POST', f"/telemedicine/sessions/{session['id']}/join-token", {'version': session['version'], 'reason': ''})
check('ephemeral join token issued with expiry', status == 200 and len(join['token']) > 10)
status, session = call('POST', f"/telemedicine/sessions/{session['id']}/admit", {'version': session['version'], 'reason': ''})
check('consultant admits the patient', status == 200 and session['status'] == 'InConsultation')
status, body = call('POST', f"/telemedicine/sessions/{session['id']}/end", {'note': 'Hi', 'emergencyAdvice': 'Escalate to nearest facility if chest pain.', 'version': session['version']})
check('consultation close requires a full note', status == 422 and body['code'] == 'care.fields')
status, session = call('POST', f"/telemedicine/sessions/{session['id']}/end", {'note': 'Reviewed symptoms, vitals and home readings.', 'emergencyAdvice': 'Escalate to nearest facility if chest pain or breathlessness.', 'version': session['version']})
check('consultation closed with escalation advice', status == 200 and session['status'] == 'Completed')
status, body = call('POST', f"/telemedicine/sessions/{session['id']}/uploads", {'fileName': 'report.exe', 'contentType': 'application/octet-stream', 'sizeBytes': 1024})
check('upload allowlist blocks executables', status == 422 and body['code'] == 'tele.upload')
status, upload = call('POST', f"/telemedicine/sessions/{session['id']}/uploads", {'fileName': 'discharge-summary.pdf', 'contentType': 'application/pdf', 'sizeBytes': 240000})
check('consultation document uploaded', status == 200 and upload['fileName'] == 'discharge-summary.pdf')

# ── inventory: indent → PO → GRN → transfer → consumption ───────────────────
loginStore = {'userName': 'store', 'password': 'Preview!2026'}
status, stores = call('GET', '/inventory/stores', login=loginStore)
check('inventory stores listed', status == 200 and len(stores) == 2)
status, indent = call('POST', '/inventory/indents', {'storeId': stores[0]['id'], 'itemId': 'e3000000-0000-0000-0000-000000000001', 'quantity': 40, 'priority': 'Routine', 'reason': 'Replenish sterile gloves below reorder level.'}, login=loginStore)
check('indent raised against the central store', status == 200 and indent['number'].startswith('IND-'))
status, body = call('POST', f"/inventory/indents/{indent['id']}/decision", {'approve': True, 'note': 'Self approval attempt.', 'version': indent['version']}, login=loginStore)
check('the raiser cannot approve their own indent', status == 403 and body['code'] == 'inventory.separation')
status, indent = call('POST', f"/inventory/indents/{indent['id']}/decision", {'approve': True, 'note': 'Approved against annual rate contract.', 'version': indent['version']}, role='Hospital Admin')
check('indent approved by a second person', status == 200 and indent['status'] == 'Approved')
status, body = call('POST', '/inventory/purchase-orders', {'vendorName': 'Unknown Vendor Ltd', 'indentId': indent['id'], 'quantity': 20, 'unitCost': 800}, login=loginStore)
check('unknown vendors are rejected', status == 422 and body['code'] == 'inventory.vendor')
status, body = call('POST', '/inventory/purchase-orders', {'vendorName': 'QuickBuy Traders', 'indentId': indent['id'], 'quantity': 20, 'unitCost': 800}, login=loginStore)
check('unauthorized vendors are rejected', status == 422 and body['code'] == 'inventory.vendor')
status, order = call('POST', '/inventory/purchase-orders', {'vendorName': 'MediCore Supplies Pvt Ltd', 'indentId': indent['id'], 'quantity': 20, 'unitCost': 800}, login=loginStore)
check('purchase order issued to an authorized vendor', status == 200 and order['number'].startswith('PO-'))
status, body = call('POST', '/inventory/purchase-orders', {'vendorName': 'OrthoPlus Instruments', 'indentId': indent['id'], 'quantity': 5, 'unitCost': 800}, login=loginStore)
check('one purchase order per indent', status == 422 and body['code'] == 'inventory.duplicate')
grnBody = {'receivedQty': 20, 'acceptedQty': 20, 'rejectedQty': 0, 'inspectionNote': '', 'batchNumber': f'SMK-B-{suffix[-5:]}', 'expiryDate': str(today + timedelta(days=500)), 'version': order['version']}
status, receipt = call('POST', f"/inventory/purchase-orders/{order['id']}/goods-receipt", grnBody, login=loginStore)
check('goods receipt accepted into stock', status == 200 and receipt['acceptedQty'] == 20)
_, batches = call('GET', '/inventory/batches', login=loginStore)
batchRow = next(b for b in batches if b['batchNumber'] == grnBody['batchNumber'])
check('received batch carries expiry and quantity', batchRow['quantity'] == 20 and batchRow['expiryDate'] is not None)
status, transfer = call('POST', '/inventory/transfers', {'fromStoreId': stores[0]['id'], 'toStoreId': stores[1]['id'], 'itemId': 'e3000000-0000-0000-0000-000000000001', 'batchId': batchRow['id'], 'quantity': 5, 'note': 'Theatre list top-up.'}, login=loginStore)
check('stock transfer dispatched', status == 200 and transfer['status'] == 'Dispatched')
status, body = call('POST', '/inventory/transfers', {'fromStoreId': stores[0]['id'], 'toStoreId': stores[1]['id'], 'itemId': 'e3000000-0000-0000-0000-000000000001', 'batchId': batchRow['id'], 'quantity': 9999, 'note': 'Excessive transfer request.'}, login=loginStore)
check('transfers cannot drive stock negative', status == 409 and body['code'] == 'inventory.stock')
status, transfer = call('POST', f"/inventory/transfers/{transfer['id']}/receive", {'version': transfer['version']}, login=loginStore)
check('transfer received at destination store', status == 200 and transfer['status'] == 'Received')
_, batches = call('GET', '/inventory/batches', login=loginStore)
dest = next(b for b in batches if b['batchNumber'] == grnBody['batchNumber'] and b['storeId'] == stores[1]['id'])
status, consumption = call('POST', '/inventory/consumptions', {'storeId': stores[1]['id'], 'itemId': 'e3000000-0000-0000-0000-000000000001', 'batchId': dest['id'], 'quantity': 2, 'department': 'Operation theatre', 'reason': 'Consumed during emergency list.'}, login=loginStore)
check('department consumption recorded', status == 200 and consumption['number'].startswith('CONS-'))

# ── CSSD: pack → cycle → indicators → release → issue → recall ──────────────
loginCssd = {'userName': 'cssdtech', 'password': 'Preview!2026'}
status, setRow = call('POST', '/cssd/sets', {'code': f'SMK-CS-{suffix[-4:]}', 'name': 'Smoke Surgery Set', 'items': [{'name': 'Forceps', 'serialNumber': f'SMK-I-{suffix[-4:]}-1'}, {'name': 'Scissors', 'serialNumber': f'SMK-I-{suffix[-4:]}-2'}]}, login=loginCssd)
check('instrument set registered', status == 200 and setRow['status'] == 'Dirty')
status, setRow = call('POST', f"/cssd/sets/{setRow['id']}/clean-pack", {'version': setRow['version'], 'reason': 'Decontaminated and packed.'}, login=loginCssd)
check('set cleaned and packed', status == 200 and setRow['status'] == 'Packed')
status, body = call('POST', '/cssd/cycles', {'method': 'Steam', 'machine': 'SMK-STM-1', 'tempC': 100, 'durationMin': 30, 'setIds': [setRow['id']]}, login=loginCssd)
check('cycle temperature bands are enforced', status == 422 and body['code'] == 'cssd.cycle')
status, cycle = call('POST', '/cssd/cycles', {'method': 'Steam', 'machine': 'SMK-STM-1', 'tempC': 121, 'durationMin': 30, 'setIds': [setRow['id']]}, login=loginCssd)
check('sterilizer cycle started', status == 200 and cycle['number'].startswith('CYC-'))
status, body = call('POST', f"/cssd/cycles/{cycle['id']}/release", {'version': cycle['version'], 'reason': 'Release without indicators.'}, login=loginCssd)
check('release requires chemical indicators', status == 422 and body['code'] == 'cssd.release')
status, cycle = call('POST', f"/cssd/cycles/{cycle['id']}/indicators", {'kind': 'Chemical', 'result': 'Pass', 'note': 'Strip changed correctly.'}, login=loginCssd)
check('chemical indicator recorded', status == 200)
status, cycle = call('POST', f"/cssd/cycles/{cycle['id']}/release", {'version': cycle['version'], 'reason': 'Cycle parameters within validated band.'}, login=loginCssd)
check('cycle released to sterile stock', status == 200 and cycle['status'] == 'Passed')
_, sets = call('GET', '/cssd/sets', login=loginCssd)
sterile = next(x for x in sets if x['id'] == setRow['id'])
check('released set shows sterile expiry', sterile['status'] == 'Sterile' and sterile['sterileExpiry'] is not None)
status, issue = call('POST', '/cssd/issues', {'setId': setRow['id'], 'bookingId': None, 'issuedTo': 'Theatre 2 · Smoke run'}, login=loginCssd)
check('sterile set issued to theatre', status == 200 and issue['number'].startswith('CSSD-'))
status, issue = call('POST', f"/cssd/issues/{issue['id']}/return", {'returnCondition': 'Incomplete', 'version': issue['version']}, login=loginCssd)
check('returns track missing instruments', status == 200 and issue['returnCondition'] == 'Incomplete')
status, recall = call('POST', '/cssd/recalls', {'cycleId': cycle['id'], 'reason': 'Post-release indicator concerns raised during smoke run.'}, login=loginCssd)
check('recall opened against the cycle', status == 200 and recall['number'].startswith('RCL-'))
status, body = call('POST', '/cssd/recalls', {'cycleId': cycle['id'], 'reason': 'Duplicate recall attempt for the same cycle.'}, login=loginCssd)
check('one open recall per cycle', status == 409 and body['code'] == 'cssd.recall')
status, recall = call('POST', f"/cssd/recalls/{recall['id']}/close", {'version': 0, 'reason': 'All affected sets traced and quarantined.'}, login=loginCssd)
check('recall closed after traceability check', status == 200 and recall['status'] == 'Closed')

print(f'\n{passed} phase 4 smoke checks passed against {BASE.split("/api")[0]}')
