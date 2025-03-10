import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const failRate = new Rate('failed_requests');
const checkoutTrend = new Trend('checkout_time');
const orderCreationTrend = new Trend('order_creation_time');
const catalogLoadTrend = new Trend('catalog_load_time');

export const options = {
  scenarios: {
    checkout_flow: {
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
    'checkout_time': ['p(95)<2000'],
    'order_creation_time': ['p(95)<1000'],
    'catalog_load_time': ['p(95)<600'],
  },
  insecureSkipTLSVerify: true,
  http2: true,
};

// Generate a random GUID in proper format
function generateGuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
    const r = Math.random() * 16 | 0, 
        v = c === 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

// Generate a random shipping address
function generateAddress() {
  const streets = ['Main St', 'Broadway', 'Park Ave', 'Market St', 'Ocean Blvd'];
  const cities = ['Seattle', 'New York', 'San Francisco', 'Los Angeles', 'Chicago'];
  const states = ['WA', 'NY', 'CA', 'CA', 'IL'];
  const countries = ['USA', 'USA', 'USA', 'USA', 'USA'];
  const zipCodes = ['98101', '10001', '94105', '90001', '60601'];
  
  const index = Math.floor(Math.random() * streets.length);
  
  return {
    street: `${Math.floor(Math.random() * 999) + 1} ${streets[index]}`,
    city: cities[index],
    state: states[index],
    country: countries[index],
    zipCode: zipCodes[index]
  };
}

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
  const orderingApiUrl = 'http://localhost:5224/api';
  const catalogApiUrl = 'http://localhost:5222/api/catalog';

  const authHeaders = {
    'Authorization': `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  };

  // Generate a unique GUID for this test iteration
  const requestId = generateGuid();
  
  // Step 1: Get catalog items like in the UI browsing flow
  const startCatalogTime = new Date();
  const itemsResponse = http.get(`${catalogApiUrl}/items?PageSize=12&PageIndex=0&api-version=1.0`);
  
  const catalogSuccess = check(itemsResponse, {
    'catalog browsing successful': (r) => r.status === 200,
    'catalog has items': (r) => r.json().data && r.json().data.length > 0,
  });
  
  failRate.add(!catalogSuccess);
  catalogLoadTrend.add(new Date() - startCatalogTime);
  
  if (!catalogSuccess || !itemsResponse.json().data || itemsResponse.json().data.length === 0) {
    console.error('Failed to retrieve catalog items, skipping checkout test');
    return;
  }
  
  // Get items from the catalog response
  const catalogItems = itemsResponse.json().data;
  
  // Step 2: Prepare order items (between 1-3 items)
  const itemCount = Math.floor(Math.random() * 3) + 1;
  let orderItems = [];
  
  for (let i = 0; i < itemCount; i++) {
    // Select a random product from the catalog response
    const randomIndex = Math.floor(Math.random() * catalogItems.length);
    const product = catalogItems[randomIndex];
    const quantity = Math.floor(Math.random() * 3) + 1;
    
    orderItems.push({
      id: "",
      productId: product.id,
      productName: product.name,
      unitPrice: product.price,
      oldUnitPrice: product.price,
      quantity: quantity,
      pictureUrl: ""
    });
  }
  
  // Step 3: Create the order directly via the Ordering API
  const address = generateAddress();
  
  // Start timing the checkout/order creation process
  const checkoutStartTime = new Date();
  
  // Create a proper DateTime string for cardExpiration (ISO format)
  const today = new Date();
  const futureDate = new Date(today.getFullYear() + 1, today.getMonth(), 1); // 1 year from now
  
  const orderRequest = {
    userId: data.userName,
    userName: data.userName,
    city: address.city,
    street: address.street,
    state: address.state,
    country: address.country,
    zipCode: address.zipCode,
    cardNumber: "1234567890123456",
    cardHolderName: "TESTUSER",
    // Use a proper ISO date format for card expiration
    cardExpiration: futureDate.toISOString(),
    cardSecurityNumber: "123",
    cardTypeId: 1,
    buyer: data.userName,
    items: orderItems
  };
  
  const createOrderRes = http.post(`${orderingApiUrl}/orders?api-version=1.0`, JSON.stringify(orderRequest), {
    headers: {
      ...authHeaders,
      'X-Requestid': requestId
    },
  });
  
  const checkoutSuccess = check(createOrderRes, {
    'order creation successful': (r) => r.status === 202 || r.status === 200,
  });
  
  failRate.add(!checkoutSuccess);
  checkoutTrend.add(new Date() - checkoutStartTime);
  orderCreationTrend.add(new Date() - checkoutStartTime);  // Same as checkout time in this direct API approach
  
  if (!checkoutSuccess) {
    console.error(`Order creation failed - Status: ${createOrderRes.status} - Request ID: ${requestId}`);
    if (createOrderRes.body) {
      console.error(`Error response: ${createOrderRes.body}`);
    }
    return;
  }
  
  // Step 4: Verify order was created
  sleep(2); // Give the system some time to process the order
  
  const ordersRes = http.get(`${orderingApiUrl}/orders?PageSize=10&PageIndex=0&api-version=1.0`, {
    headers: authHeaders,
  });
  
  const orderVerified = check(ordersRes, {
    'order list retrieved successfully': (r) => r.status === 200,
    'order exists in order history': (r) => {
      try {
        const orders = JSON.parse(r.body);
        return orders && orders.length > 0;
      } catch {
        return false;
      }
    },
  });
  
  failRate.add(!orderVerified);
  
  // Add a pause between test iterations
  sleep(Math.random() * 3 + 2); // Sleep between 2-5 seconds
}
