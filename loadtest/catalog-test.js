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
        { duration: '30s', target: 10 },   // Ramp up to 10 users
        { duration: '1m', target: 10 },    // Stay at 10 users for 1 minute
        { duration: '30s', target: 50 },   // Ramp up to 50 users
        { duration: '1m', target: 50 },    // Stay at 50 users for 1 minute
        { duration: '30s', target: 0 },    // Ramp down to 0 users
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<200'],      // 95% of requests should complete within 200ms
    'failed_requests': ['rate<0.05'],      // Less than 5% of requests should fail
  },
  // Disable certificate validation for development/testing
  insecureSkipTLSVerify: true,
};

export default function() {
  // Configuration
  const catalogApiUrl = 'http://localhost:5222/api/catalog';

  // Make request to get catalog items
  const itemsResponse = http.get(`${catalogApiUrl}/items?PageSize=10&PageIndex=0&api-version=1.0`);

  // Check if the request was successful
  const itemsSuccess = check(itemsResponse, {
    'catalog items status is 200': (r) => r.status === 200,
    'catalog has items': (r) => r.json().data.length > 0,
  });

  failRate.add(!itemsSuccess);

  // Get a random item ID if items were returned successfully
  if (itemsSuccess && itemsResponse.json().data.length > 0) {
    const items = itemsResponse.json().data;
    const randomIndex = Math.floor(Math.random() * items.length);
    const itemId = items[randomIndex].id;

    // Make request to get item details
    const itemDetailResponse = http.get(`${catalogApiUrl}/items/${itemId}?api-version=1.0`);

    // Check if the item detail request was successful
    const itemDetailSuccess = check(itemDetailResponse, {
      'item detail status is 200': (r) => r.status === 200,
      'item detail has name': (r) => r.json().name !== undefined,
    });

    failRate.add(!itemDetailSuccess);

    // Get catalog brands
    const brandsResponse = http.get(`${catalogApiUrl}/catalogbrands?api-version=1.0`);

    // Check if the brands request was successful
    const brandsSuccess = check(brandsResponse, {
      'brands status is 200': (r) => r.status === 200,
      'has brands': (r) => Array.isArray(r.json()),
    });

    failRate.add(!brandsSuccess);

    // Get catalog types
    const typesResponse = http.get(`${catalogApiUrl}/catalogtypes?api-version=1.0`);

    // Check if the types request was successful
    const typesSuccess = check(typesResponse, {
      'types status is 200': (r) => r.status === 200,
      'has types': (r) => Array.isArray(r.json()),
    });

    failRate.add(!typesSuccess);
  }

  // Wait between iterations
  sleep(1);
}

// Add a setup function to validate connection before the test
export function setup() {
  const catalogApiUrl = 'http://localhost:5222/api/catalog';
  const res = http.get(`${catalogApiUrl}/items?PageSize=1&PageIndex=0&api-version=1.0`);

  console.log(res.body)
  const success = check(res, {
    'Catalog API is reachable': (r) => r.status === 200,
  });

  if (!success) {
    throw new Error('Cannot reach Catalog API. Please check if the service is running.');
  }

  return {};
}
