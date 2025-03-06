import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const failRate = new Rate('failed_requests');

export const options = {
  scenarios: {
    catalog_browsing: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },
        { duration: '1m', target: 10 },
        { duration: '30s', target: 50 },
        { duration: '1m', target: 50 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<200'],
    'failed_requests': ['rate<0.05'],
  },
  insecureSkipTLSVerify: true,
};

export default function() {
  const catalogApiUrl = 'http://localhost:5222/api/catalog';

  const itemsResponse = http.get(`${catalogApiUrl}/items?PageSize=10&PageIndex=0&api-version=1.0`);

  const itemsSuccess = check(itemsResponse, {
    'catalog items status is 200': (r) => r.status === 200,
    'catalog has items': (r) => r.json().data.length > 0,
  });

  failRate.add(!itemsSuccess);

  if (itemsSuccess && itemsResponse.json().data.length > 0) {
    const items = itemsResponse.json().data;
    const randomIndex = Math.floor(Math.random() * items.length);
    const itemId = items[randomIndex].id;

    const itemDetailResponse = http.get(`${catalogApiUrl}/items/${itemId}?api-version=1.0`);

    const itemDetailSuccess = check(itemDetailResponse, {
      'item detail status is 200': (r) => r.status === 200,
      'item detail has name': (r) => r.json().name !== undefined,
    });

    failRate.add(!itemDetailSuccess);

    const brandsResponse = http.get(`${catalogApiUrl}/catalogbrands?api-version=1.0`);

    const brandsSuccess = check(brandsResponse, {
      'brands status is 200': (r) => r.status === 200,
      'has brands': (r) => Array.isArray(r.json()),
    });

    failRate.add(!brandsSuccess);

    const typesResponse = http.get(`${catalogApiUrl}/catalogtypes?api-version=1.0`);

    const typesSuccess = check(typesResponse, {
      'types status is 200': (r) => r.status === 200,
      'has types': (r) => Array.isArray(r.json()),
    });

    failRate.add(!typesSuccess);
  }

  sleep(1);
}

export function setup() {
  const catalogApiUrl = 'http://localhost:5222/api/catalog';
  const res = http.get(`${catalogApiUrl}/items?PageSize=1&PageIndex=0&api-version=1.0`);

  const success = check(res, {
    'Catalog API is reachable': (r) => r.status === 200,
  });

  if (!success) {
    throw new Error('Cannot reach Catalog API. Please check if the service is running.');
  }

  return {};
}
