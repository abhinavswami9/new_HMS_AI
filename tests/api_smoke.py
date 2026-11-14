#!/usr/bin/env python3
"""HTTP security/workflow smoke tests against an explicitly synthetic Demo API.
Never point this script at a production hospital. Uses only the Python standard library.
"""
import json
import os
import time
import urllib.request
import urllib.error
import http.cookiejar

BASE = os.environ.get('CAREFLOW_URL', 'http://127.0.0.1:5173') + '/api/v1'
assert 'localhost' in BASE or '127.0.0.1' in BASE, 'Smoke tests are restricted to loopback demo hosts.'
API_ROOT = os.environ.get('CAREFLOW_API_ROOT', 'http://127.0.0.1:5080')
assert 'localhost' in API_ROOT or '127.0.0.1' in API_ROOT, 'Smoke tests are restricted to loopback demo hosts.'
jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
passed = 0

def call(method, path, body=None, authenticated=True, headers=None):
    h = {'X-Requested-With': 'Careflow', 'Origin': BASE.split('/api')[0]}
    if body is not None:
        h['Content-Type'] = 'application/json'
    h.update(headers or {})
    req = urllib.request.Request(BASE + path, data=None if body is None else json.dumps(body).encode(), headers=h, method=method)
    opener = client if authenticated else urllib.request.build_opener()
    try:
        with opener.open(req, timeout=15) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None, response.headers
    except urllib.error.HTTPError as error:
        raw = error.read()
        return error.code, json.loads(raw) if raw else None, error.headers

def check(name, condition):
    global passed
    assert condition, name
    passed += 1
    print('PASS', name)

def fetch(path):
    req = urllib.request.Request(API_ROOT + path, headers={'X-Requested-With': 'Careflow'}, method='GET')
    try:
        with urllib.request.build_opener().open(req, timeout=15) as response:
            return response.status, response.read().decode('utf-8', 'replace'), response.headers
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode('utf-8', 'replace'), error.headers

config = call('GET', '/config', authenticated=False)
check('public config identifies synthetic mode', config[0] == 200 and config[1]['demo'] is True)

status, body, headers = fetch('/')
check('ASP.NET Core host serves the compiled workspace', status == 200 and 'text/html' in headers.get('Content-Type', '') and 'id="root"' in body)
status, body, headers = fetch('/workspace/patients/deep-link')
check('SPA fallback answers deep links with the application shell', status == 200 and 'id="root"' in body)
status, body, headers = fetch('/api/v1/definitely-not-a-real-endpoint')
check('unknown API paths return JSON errors, never the SPA shell', status == 401 and 'application/json' in headers.get('Content-Type', '') and 'id="root"' not in body)
check('unauthenticated workspace is denied', call('GET', '/workspace', authenticated=False)[0] == 401)
status, user, headers = call('POST', '/auth/demo', {})
check('demo session has HttpOnly and SameSite cookies', status == 200 and 'httponly' in headers.get('Set-Cookie', '').lower() and 'samesite=strict' in headers.get('Set-Cookie', '').lower())
check('write without CSRF header is denied', call('POST', '/patients', {}, headers={'X-Requested-With': ''})[0] == 403)
check('cross-origin write is denied', call('POST', '/patients', {}, headers={'Origin': 'https://untrusted.example'})[0] == 403)
check('failed login has generic authentication error', call('POST', '/auth/login', {'userName': 'admin', 'password': 'incorrect'})[0] == 401)
call('POST', '/auth/demo', {})
workspace = call('GET', '/workspace')[1]['workspace']
patient = next(p for p in workspace['patients'] if p['uhid'].endswith('-001241'))
service = workspace['services'][0]
status, invoice, _ = call('POST', '/invoices', {'patientId': patient['id'], 'lines': [{'serviceId': service['id'], 'quantity': 1}]})
check('invoice accepts optional encounter omission', status == 200 and invoice['total'] == service['price'])
status, invoice, _ = call('POST', f"/invoices/{invoice['id']}/issue", {'version': invoice['version']})
check('draft invoice issues once', status == 200 and invoice['status'] == 'Issued')
check('overpayment is rejected', call('POST', f"/invoices/{invoice['id']}/payments", {'amount': invoice['total'] + 1, 'method': 'Cash', 'reference': 'SMOKE-OVER', 'version': invoice['version']})[0] == 422)
reference = 'SMOKE-' + str(time.time_ns())
payment = {'amount': invoice['total'], 'method': 'Cash', 'reference': reference, 'version': invoice['version']}
status, paid, _ = call('POST', f"/invoices/{invoice['id']}/payments", payment)
check('payment settles invoice', status == 200 and paid['status'] == 'Paid')
status, retry, _ = call('POST', f"/invoices/{invoice['id']}/payments", payment)
check('stale-version identical payment retry is idempotent', status == 200 and len(retry['payments']) == 1)
check('missing required JSON fields are rejected', call('POST', '/invoices', {'patientId': patient['id']})[0] == 400)
abha_suffix = str(time.time_ns())[-6:]
status, body, _ = call('POST', '/patients', {'firstName': 'Smoke', 'lastName': 'Abha', 'dateOfBirth': '1990-01-01', 'sex': 'Female', 'phone': '+91 9000' + abha_suffix, 'abhaNumber': '123'})
check('ABHA number must be 14 digits', status == 422 and body['code'] == 'patient.fields')
status, body, _ = call('POST', '/patients', {'firstName': 'Smoke', 'dateOfBirth': '1990-01-01', 'lastName': 'Abha', 'sex': 'Female', 'phone': '+91 9000' + abha_suffix, 'aadhaarNumber': '781234567890'})
check('Aadhaar requires an ABHA linkage', status == 422 and body['code'] == 'patient.abha')
status, registered, _ = call('POST', '/patients', {'firstName': 'Smoke', 'lastName': 'Demographics', 'dateOfBirth': '1990-01-01', 'sex': 'Female', 'phone': '+91 9000' + abha_suffix, 'title': 'Mrs', 'maritalStatus': 'Married', 'fatherName': 'R. Smoke', 'city': 'Pune', 'state': 'Maharashtra', 'pincode': '411001', 'abhaNumber': '12345678901234', 'abhaAddress': 'smoke.demographics@abdm', 'aadhaarNumber': '781234567890'})
check('demographics and ABHA linkage are accepted', status == 200 and registered['abhaNumber'] == '12345678901234' and registered['city'] == 'Pune' and registered['pincode'] == '411001')
call('POST', '/auth/demo', {'role': 'Nurse'})
check('nurse cannot create invoices', call('POST', '/invoices', {'patientId': patient['id'], 'lines': []})[0] == 403)
check('nurse cannot read audit events', len(call('GET', '/workspace')[1]['workspace']['audit']) == 0)
call('POST', '/auth/demo', {'role': 'Doctor'})
check('doctor cannot collect payments', call('POST', f"/invoices/{invoice['id']}/payments", payment)[0] == 403)
call('POST', '/auth/demo', {})
check('financial mutation is audited', any(a['entityId'] == invoice['id'] and a['action'] == 'invoice.payment.recorded' for a in call('GET', '/workspace')[1]['workspace']['audit']))
check('logout succeeds', call('POST', '/auth/logout', {})[0] == 204)
check('revoked session cannot read workspace', call('GET', '/workspace')[0] == 401)
print(f'\n{passed} HTTP smoke checks passed.')
