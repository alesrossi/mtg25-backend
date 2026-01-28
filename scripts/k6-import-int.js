import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'https://api.167.235.109.6.nip.io';
const LOGIN_URL = __ENV.LOGIN_URL || `${BASE_URL}/api/accounts/login`;
const IMPORT_URL = __ENV.IMPORT_URL || `${BASE_URL}/api/decks/import`;
const METHOD = (__ENV.METHOD || 'POST').toUpperCase();
const EMAIL = __ENV.LOGIN_EMAIL || 'test@example.com';
const PASSWORD = __ENV.LOGIN_PASSWORD || 'Password123!';
const BODY = __ENV.BODY || '';
const BODY_FILE = __ENV.BODY_FILE || `${__ENV.PWD || ''}/scripts/body.json`;
const FILE_BODY = BODY ? null : open(BODY_FILE);

export const options = {
  vus: __ENV.VUS ? parseInt(__ENV.VUS, 10) : 5,
  duration: __ENV.DURATION || '30s',
  thresholds: {
    http_req_failed: ['rate<0.01'],
  },
};

export function setup() {
  const loginPayload = JSON.stringify({ email: EMAIL, password: PASSWORD });
  const loginRes = http.post(LOGIN_URL, loginPayload, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(loginRes, { 'login status 200': (r) => r.status === 200 });

  const body = loginRes.json();
  const token = body.token || body.accessToken || body.jwt;

  if (!token) {
    throw new Error(`Login did not return a token. Response: ${loginRes.body}`);
  }

  let importBody = null;
  if (METHOD !== 'GET' && METHOD !== 'HEAD') {
    importBody = BODY ? BODY : FILE_BODY;
  }

  return { token, importBody, method: METHOD };
}

export default function (data) {
  const res = http.request(data.method, IMPORT_URL, data.importBody, {
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${data.token}`,
    },
  });

  check(res, {
    'import status 2xx': (r) => r.status >= 200 && r.status < 300,
  });

  sleep(1);
}
