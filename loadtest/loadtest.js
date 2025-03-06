import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const failRate = new Rate('failed_requests');

export const options = {
  scenarios: {
    login_performance: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '1m', target: 5 },
        { duration: '30s', target: 20 },
        { duration: '1m', target: 20 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],
    'failed_requests': ['rate<0.1'],
  },
  insecureSkipTLSVerify: true,
};

export default function() {
  const identityServerUrl = 'https://localhost:5243';
  const clientId = 'loadtest';
  const clientSecret = 'secret';
  const username = 'alice';
  const password = 'Pass123$';
  const tokenUrl = `${identityServerUrl}/connect/token`;

  const payload = {
    client_id: clientId,
    client_secret: clientSecret,
    grant_type: 'password',
    username: username,
    password: password,
    scope: 'openid profile orders basket'
  };

  const params = {
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
  };

  const response = http.post(tokenUrl, payload, params);

  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'has access token': (r) => r.json('access_token') !== undefined,
  });

  failRate.add(!success);

  if (success) {
    /*
    const token = response.json('access_token');
    const apiResponse = http.get(`${apiUrl}/some-endpoint`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    check(apiResponse, {
      'API request successful': (r) => r.status === 200,
    });
    */
  }

  sleep(1);
}

export function setup() {
  const identityServerUrl = 'https://localhost:5243';
  const res = http.get(`${identityServerUrl}/.well-known/openid-configuration`);

  const success = check(res, {
    'Identity Server is reachable': (r) => r.status === 200,
  });

  if (!success) {
    throw new Error('Cannot reach Identity Server. Please check the URL and try again.');
  }

  return {};
}
