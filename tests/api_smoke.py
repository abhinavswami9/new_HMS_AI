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

config = call('GET', '/config', authenticated=False)
check('public config identifies synthetic mode', config[0] == 200 and config[1]['demo'] is True)
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
