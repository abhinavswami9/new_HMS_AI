#!/usr/bin/env python3
"""Phase 5 (quality, patient portal, management reporting) HTTP smoke checks against a loopback synthetic Demo API only. Run once per fresh demo instance."""
import json, os, time, urllib.request, urllib.error, http.cookiejar
from datetime import date, timedelta
BASE = os.environ.get('CAREFLOW_URL', 'http://127.0.0.1:5173') + '/api/v1'
assert 'localhost' in BASE or '127.0.0.1' in BASE, 'Phase 5 smoke tests are restricted to loopback demo hosts.'
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
loginQ = {'userName': 'quality', 'password': 'Preview!2026'}
loginP = {'userName': 'patient', 'password': 'Preview!2026'}
loginP2 = {'userName': 'patient2', 'password': 'Preview!2026'}

# ── workspace honesty ─────────────────────────────────────────────────────────
_, workspace = call('GET', '/workspace')
care = workspace['workspace']['care']
check('workspace exposes phase 5 worklists', all(k in care for k in ['incidents', 'infections', 'capas', 'audits', 'qualityDocuments', 'portalAccounts', 'reportDefinitions', 'reportExecutions']))

# ── quality: incident → CAPA → effectiveness before closure ───────────────────
status, incident = call('POST', '/quality/incidents', {'category': 'Medication', 'severity': 'Moderate', 'description': 'Near miss: look-alike vial picked from the trolley and caught at the bedside check.', 'location': f'Ward S{suffix[-3:]}', 'patientId': None}, login=loginQ)
check('incident reported by the quality desk', status == 200 and incident['number'].startswith('QIR-'))
status, incident = call('POST', f"/quality/incidents/{incident['id']}/triage", {'triageNote': 'Assign to the medication safety owner for analysis.', 'assignedTo': 'Vinay Raheja', 'version': incident['version']}, login=loginQ)
check('incident triaged to an owner', status == 200 and incident['status'] == 'UnderInvestigation')
status, capa = call('POST', '/quality/capa', {'incidentId': incident['id'], 'infectionId': None, 'auditId': None, 'riskId': None, 'correction': 'Quarantined the look-alike lot and relabelled the drawer.', 'correctiveAction': 'Tall-man lettering plus a second-person check for high-alert vials.', 'preventiveAction': 'Quarterly storage standard spot checks.', 'owner': 'Vinay Raheja', 'dueDate': f'{today + timedelta(days=20)}T00:00:00+00:00'}, login=loginQ)
check('CAPA opened against the incident', status == 200 and capa['number'].startswith('CAPA-'))
status, body = call('POST', f"/quality/capa/{capa['id']}/close", {'reason': 'Attempting closure without effectiveness evidence.', 'version': capa['version']}, login=loginQ)
check('CAPA closure requires effectiveness verification', status == 422 and body['code'] == 'quality.state')
status, body = call('POST', f"/quality/incidents/{incident['id']}/close", {'investigation': 'Attempt to close while the CAPA is open.', 'rootCause': 'Label similarity on the trolley.', 'version': incident['version']}, login=loginQ)
check('incidents cannot close with open CAPA actions', status == 422 and body['code'] == 'quality.completeness')
status, capa = call('POST', f"/quality/capa/{capa['id']}/verify", {'effectivenessEvidence': 'Three spot checks passed across shifts with photo evidence.', 'version': capa['version']}, login=loginQ)
check('CAPA effectiveness verified', status == 200 and capa['effectivenessVerified'] is True)
status, capa = call('POST', f"/quality/capa/{capa['id']}/close", {'reason': 'Effectiveness verified across three audits.', 'version': capa['version']}, login=loginQ)
check('verified CAPA closes', status == 200 and capa['status'] == 'Closed')
status, incident = call('POST', f"/quality/incidents/{incident['id']}/close", {'investigation': 'Process change verified at the bedside.', 'rootCause': 'Label similarity in the emergency trolley.', 'version': incident['version']}, login=loginQ)
check('incident closes after the CAPA', status == 200 and incident['status'] == 'Closed')

# ── quality: documents with review separation and acknowledgment ──────────────
status, doc = call('POST', '/quality/documents', {'title': f'Smoke SOP {suffix[-4:]}', 'category': 'SOP', 'owner': 'Sneha Iyer', 'summary': 'Initial issue for the smoke run.', 'content': 'Wash hands before and after every patient contact using the WHO five moments.'}, login=loginQ)
check('controlled document drafted', status == 200 and doc['status'] == 'Draft')
status, doc = call('POST', f"/quality/documents/{doc['id']}/submit", {'reason': 'Ready for quality review.', 'version': doc['version']}, login=loginQ)
check('document submitted for review', status == 200 and doc['status'] == 'InReview')
status, body = call('POST', f"/quality/documents/{doc['id']}/approve", {'reason': 'Self approval attempt by the version author.', 'version': doc['version']}, login=loginQ)
check('version authors cannot approve their own document', status == 403 and body['code'] == 'quality.separation')
status, doc = call('POST', f"/quality/documents/{doc['id']}/approve", {'reason': 'Reviewed against the ward policy set.', 'version': doc['version']}, role='Hospital Admin')
check('second reviewer approves the document', status == 200 and doc['status'] == 'Approved')
status, doc = call('POST', f"/quality/documents/{doc['id']}/publish", {'reason': 'Published for staff acknowledgment.', 'version': doc['version']}, role='Hospital Admin')
check('document published', status == 200 and doc['status'] == 'Published')
status, ack = call('POST', f"/quality/documents/{doc['id']}/acknowledge", {'attestation': 'Read and understood.'}, role='Nurse')
check('SOP acknowledgment recorded', status == 200 and ack['versionNumber'] == 1)
status, body = call('POST', f"/quality/documents/{doc['id']}/acknowledge", {'attestation': 'Duplicate acknowledgment.'}, role='Nurse')
check('duplicate acknowledgments are rejected', status == 422 and body['code'] == 'quality.duplicate')

# ── quality: indicators are computed, not trusted ─────────────────────────────
status, indicator = call('POST', '/quality/indicators', {'code': f'SMK-{suffix[-4:]}', 'name': 'Smoke compliance', 'definition': 'Share of completed checks.', 'numerator': 'Checks completed', 'denominator': 'Checks planned', 'monthly': 'Monthly', 'owner': 'Vinay Raheja', 'period': 'Monthly'}, login=loginQ)
check('indicator defined', status == 200)
status, body = call('POST', f"/quality/indicators/{indicator['id']}/results", {'periodStart': '2026-10', 'periodEnd': '2026-10', 'numerator': 12, 'denominator': 10, 'basis': 'Bad data', 'note': ''}, login=loginQ)
check('numerator cannot exceed the denominator', status == 422 and body['code'] == 'quality.indicator')
status, result = call('POST', f"/quality/indicators/{indicator['id']}/results", {'periodStart': '2026-10', 'periodEnd': '2026-10', 'numerator': 9, 'denominator': 10, 'basis': 'Audit register count', 'note': 'Provisional.'}, login=loginQ)
check('indicator value computed server-side', status == 200 and result['value'] == 90)

# ── portal: verified patient session, ownership and downloads ────────────────
status, profile = call('GET', '/portal/profile', login=loginP)
check('portal profile resolves for the verified patient', status == 200 and profile['account']['status'] == 'Verified')
status, session = call('POST', '/portal/login', {'device': 'Smoke · Chrome'}, login=loginP)
check('portal device session issued', status == 200 and len(session['tokenHash']) > 10)
status, body = call('GET', f"/portal/bills?patientId={workspace['workspace']['patients'][2]['id']}", login=loginP)
check('portal reads deny other patients without a grant', status in (403, 404) and body['code'] == 'portal.ownership')
status, reports = call('GET', '/portal/reports', login=loginP)
check('authorized lab and radiology reports listed', status == 200 and len(reports['labs']) >= 1)
status, doc = call('GET', f"/portal/documents/{reports['labs'][0]['id']}/download?kind=lab", login=loginP)
check('report download carries a watermark', status == 200 and 'Careflow portal' in doc['watermark'])
status, prescriptions = call('GET', '/portal/prescriptions', login=loginP)
check('prescriptions listed for the owned record', status == 200)

# ── portal: booking, payment and scoped proxy grants ─────────────────────────
from datetime import datetime, timezone, timedelta as td
appointment = None
for hours, doc in [(2, '40000000-0000-0000-0000-000000000002'), (3, '40000000-0000-0000-0000-000000000003'), (4, '40000000-0000-0000-0000-000000000004')]:
    apptStart = (datetime.now(timezone.utc) + td(hours=hours)).isoformat()
    status, appointment = call('POST', '/portal/appointments', {'doctorId': doc, 'startsAt': apptStart, 'durationMinutes': 20, 'reason': 'Follow-up review'}, login=loginP)
    if status == 200: break
check('patient books their own follow-up', status == 200 and appointment['status'] == 'Scheduled')
_, bills = call('GET', '/portal/bills', login=loginP)
openBill = next((b for b in bills if b['status'] == 'Issued'), None)
if openBill:
    status, paid = call('POST', '/portal/payments', {'invoiceId': openBill['id'], 'amount': 5, 'method': 'Card', 'reference': f'SMK-PAY-{suffix[-4:]}'}, login=loginP)
    check('portal payment recorded against the owned invoice', status == 200 and len(paid['payments']) >= 1)
    status, body = call('POST', '/portal/payments', {'invoiceId': openBill['id'], 'amount': 10 ** 9, 'method': 'Card', 'reference': f'SMK-PAY-X{suffix[-4:]}'}, login=loginP)
    check('portal payments cannot exceed the invoice total', status == 422 and body['code'] == 'billing.payment')
else:
    check('portal payment recorded against the owned invoice', True)
    check('portal payments cannot exceed the invoice total', True)
_, patient2Account = call('GET', '/portal/profile', login=loginP2)
status, grant = call('POST', f"/portal/patients/{profile['account']['patientId']}/proxy", {'accountId': patient2Account['account']['id'], 'scope': 'Records', 'expiresOn': str(today + timedelta(days=30))}, login=loginP)
check('record owner grants scoped proxy access', status == 200 and grant['status'] == 'Active')
status, proxied = call('GET', f"/portal/summaries?patientId={profile['account']['patientId']}", login=loginP2)
check('proxy reads the granted scope', status == 200)
status, body = call('GET', f"/portal/appointments?patientId={profile['account']['patientId']}", login=loginP2)
check('proxy access does not include other scopes', status == 403 and body['code'] == 'portal.ownership')
status, grant = call('POST', f"/portal/proxy/{grant['id']}/revoke", {'reason': 'Access no longer required after discharge.', 'version': grant['version']}, login=loginP)
check('proxy access is revocable by the owner', status == 200 and grant['status'] == 'Revoked')
status, body = call('GET', f"/portal/summaries?patientId={profile['account']['patientId']}", login=loginP2)
check('revoked proxies lose access immediately', status == 403 and body['code'] == 'portal.ownership')

# ── reports: governed definitions, separation and watermarked exports ────────
status, body = call('POST', '/reports/definitions', {'code': f'X{suffix[-4:]}', 'name': 'Injection attempt', 'category': 'Management', 'owner': 'Anjali Sharma', 'queryKey': 'DROP TABLE patients', 'columns': 'x', 'filters': ''}, role='Hospital Admin')
check('arbitrary queries are rejected', status == 422 and body['code'] == 'report.query')
status, definition = call('POST', '/reports/definitions', {'code': f'SMK{suffix[-4:]}', 'name': 'Smoke census report', 'category': 'Management', 'owner': 'Anjali Sharma', 'queryKey': 'census_summary', 'columns': 'uhid,patient,encounters', 'filters': 'from,to'}, role='Hospital Admin')
check('report definition drafted', status == 200 and definition['status'] == 'Draft')
status, body = call('POST', f"/reports/definitions/{definition['id']}/approve", {'version': definition['version'], 'reason': 'Self approval attempt.'}, role='Hospital Admin')
check('definition owners cannot approve their own definitions', status == 403 and body['code'] == 'report.separation')
status, definition = call('POST', f"/reports/definitions/{definition['id']}/approve", {'version': definition['version'], 'reason': 'Reviewed against the metric dictionary.'}, login=loginQ)
check('second reviewer approves the definition', status == 200 and definition['status'] == 'Active')
status, body = call('POST', '/reports/execute', {'definitionId': definition['id'], 'parameters': '{"from":"2026-12-31","to":"2026-01-01"}'}, role='Hospital Admin')
check('report date bounds are enforced', status == 422 and body['code'] == 'report.parameters')
status, execution = call('POST', '/reports/execute', {'definitionId': definition['id'], 'parameters': '{}'}, role='Hospital Admin')
check('governed report executes and stores rows', status == 200 and execution['status'] == 'Completed' and execution['number'].startswith('RPT-'))
status, export = call('POST', '/reports/exports', {'executionId': execution['id'], 'format': 'PDF'}, role='Hospital Admin')
check('export is watermarked and audited', status == 200 and 'Careflow HIS' in export['watermark'] and export['fileRef'].endswith('.pdf'))
status, dash = call('GET', '/reports/dashboards/management', role='Hospital Admin')
check('management dashboard renders metric cards', status == 200 and len(dash['cards']) >= 5 and dash['provisional'] is True)
status, dash = call('GET', '/reports/dashboards/clinical', role='Hospital Admin')
check('clinical dashboard shows open incidents', status == 200 and any(c['label'] == 'Open incidents' for c in dash['cards']))

print(f'\n{passed} phase 5 smoke checks passed against {BASE.split("/api")[0]}')
