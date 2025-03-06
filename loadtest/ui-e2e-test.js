import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const failRate = new Rate('failed_requests');
const pageLoadTrend = new Trend('page_load_time');
const detailsLoadTrend = new Trend('details_load_time');

export const options = {
  scenarios: {
    ui_browsing_flow: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },
        { duration: '1m', target: 25 },
        { duration: '30s', target: 50 },
        { duration: '1m', target: 50 },
        { duration: '30s', target: 0 },
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<800'],
    'failed_requests': ['rate<0.1'],
    'page_load_time': ['p(95)<1000'],
    'details_load_time': ['p(95)<600'],
  },
  insecureSkipTLSVerify: true,
  http2: true,
};

export function setup() {
  const identityServerUrl = 'https://localhost:5243';
  const tokenUrl = `${identityServerUrl}/connect/token`;

  const payload = {
    client_id: 'loadtest',
    client_secret: 'secret',
    grant_type: 'password',
    username: 'alice',
    password: 'Pass123$',
    scope: 'openid profile orders'
  };

  const params = {
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
    },
  };

  const tokenResponse = http.post(tokenUrl, payload, params);

  const tokenSuccess = check(tokenResponse, {
    'token request successful': (r) => r.status === 200,
    'has access token': (r) => r.json('access_token') !== undefined,
  });

  if (!tokenSuccess) {
    throw new Error('Failed to get authentication token. Please check if Identity service is running.');
  }

  const webAppUrl = 'https://localhost:7298';
  const webResponse = http.get(webAppUrl);

  const webSuccess = check(webResponse, {
    'Web UI is reachable': (r) => r.status === 200,
  });

  if (!webSuccess) {
    throw new Error('Cannot reach Web UI. Please check if the Web application is running.');
  }

  const catalogApiUrl = 'http://localhost:5222/api/catalog';
  const catalogResponse = http.get(`${catalogApiUrl}/items?PageSize=1&PageIndex=0&api-version=1.0`);

  const catalogSuccess = check(catalogResponse, {
    'Catalog API is reachable': (r) => r.status === 200,
  });

  if (!catalogSuccess) {
    throw new Error('Cannot reach Catalog API. Please check if the Catalog service is running.');
  }

  const orderingApiUrl = 'http://localhost:5224/api';
  const orderingResponse = http.get(`${orderingApiUrl}/orders?PageSize=1&PageIndex=0&api-version=1.0`, {
    headers: {
      'Authorization': `Bearer ${tokenResponse.json('access_token')}`,
    },
  });

  const orderingSuccess = check(orderingResponse, {
    'Ordering API is reachable': (r) => r.status === 200 || r.status === 401,
  });

  if (!orderingSuccess) {
    throw new Error('Cannot reach Ordering API. Please check if the Ordering service is running.');
  }

  return {
    token: tokenResponse.json('access_token'),
    userName: payload.username,
  };
}

export default function(data) {
  const webAppUrl = 'https://localhost:7298';
  const catalogApiUrl = 'http://localhost:5222/api/catalog';
  const orderingApiUrl = 'http://localhost:5224/api';

  const authHeaders = {
    'Authorization': `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  };

  const startHomeTime = new Date();
  const homeResponse = http.get(webAppUrl, {
    headers: {
      'Accept': 'text/html',
    }
  });
  
  const homeSuccess = check(homeResponse, {
    'home page loaded successfully': (r) => r.status === 200,
  });
  
  failRate.add(!homeSuccess);
  const endHomeTime = new Date();
  pageLoadTrend.add(endHomeTime - startHomeTime);
  
  const startBrowsingTime = new Date();
  const itemsResponse = http.get(`${catalogApiUrl}/items?PageSize=12&PageIndex=0&api-version=1.0`);
  
  const browsingSuccess = check(itemsResponse, {
    'catalog browsing successful': (r) => r.status === 200,
    'catalog has items': (r) => r.json().data && r.json().data.length > 0,
  });
  
  failRate.add(!browsingSuccess);
  
  const endBrowsingTime = new Date();
  pageLoadTrend.add(endBrowsingTime - startBrowsingTime);

  if (browsingSuccess && itemsResponse.json().data.length > 0) {
    const items = itemsResponse.json().data;
    const randomIndex = Math.floor(Math.random() * items.length);
    const item = items[randomIndex];
    
    const startDetailTime = new Date();
    const itemDetailResponse = http.get(`${catalogApiUrl}/items/${item.id}?api-version=1.0`);
    const endDetailTime = new Date();
    
    detailsLoadTrend.add(endDetailTime - startDetailTime);
    
    const itemDetailSuccess = check(itemDetailResponse, {
      'item detail loaded successfully': (r) => r.status === 200,
      'item has name': (r) => r.json().name !== undefined,
    });
    
    failRate.add(!itemDetailSuccess);
    
    const brandsResponse = http.get(`${catalogApiUrl}/catalogbrands?api-version=1.0`);
    const typesResponse = http.get(`${catalogApiUrl}/catalogtypes?api-version=1.0`);
    
    check(brandsResponse, {
      'brands loaded successfully': (r) => r.status === 200,
      'has brands': (r) => Array.isArray(r.json()),
    });
    
    check(typesResponse, {
      'types loaded successfully': (r) => r.status === 200,
      'has types': (r) => Array.isArray(r.json()),
    });
    
    const ordersResponse = http.get(`${orderingApiUrl}/orders?PageSize=10&PageIndex=0&api-version=1.0`, {
      headers: authHeaders
    });
    
    check(ordersResponse, {
      'order history loaded': (r) => r.status === 200,
    });
  }
  
  sleep(2);
}