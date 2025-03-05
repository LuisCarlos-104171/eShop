import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const failRate = new Rate('failed_requests');
const checkoutTrend = new Trend('checkout_time');

export const options = {
  scenarios: {
    checkout_flow: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },    // Ramp up to 5 users
        { duration: '1m', target: 5 },     // Stay at 5 users for 1 minute
        { duration: '30s', target: 15 },   // Ramp up to 15 users
        { duration: '1m', target: 15 },    // Stay at 15 users for 1 minute
        { duration: '30s', target: 0 },    // Ramp down to 0 users
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<500'],      // 95% of requests should complete within 500ms
    'failed_requests': ['rate<0.05'],      // Less than 5% of requests should fail
    'checkout_time': ['p(95)<800'],        // 95% of checkout operations under 800ms
  },
  // Disable certificate validation for development/testing
  insecureSkipTLSVerify: true,
};

export function setup() {
  // Get token for authentication
  const identityServerUrl = 'https://localhost:5243';
  const tokenUrl = `${identityServerUrl}/connect/token`;
  
  const payload = {
    client_id: 'loadtest',
    client_secret: 'secret',
    grant_type: 'client_credentials',
    scope: 'orders basket'
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
  
  // Check if Basket API is reachable
  const basketApiUrl = 'http://localhost:5223/api/v1';
  const testUserId = 'test-user-setup';
  
  const basketResponse = http.get(`${basketApiUrl}/basket/${testUserId}`);
  
  const basketSuccess = check(basketResponse, {
    'Basket API is reachable': (r) => r.status === 200 || r.status === 404, // 404 is acceptable since the basket may not exist
  });
  
  if (!basketSuccess) {
    throw new Error('Cannot reach Basket API. Please check if the service is running.');
  }
  
  // Check if Ordering API is reachable
  const orderingApiUrl = 'http://localhost:5133/api/v1';
  const orderingResponse = http.get(`${orderingApiUrl}/orders?pageSize=1&pageIndex=0`, {
    headers: {
      'Authorization': `Bearer ${tokenResponse.json('access_token')}`,
    },
  });
  
  const orderingSuccess = check(orderingResponse, {
    'Ordering API is reachable': (r) => r.status === 200 || r.status === 401, // 401 is acceptable if token is not valid for this endpoint
  });
  
  if (!orderingSuccess) {
    throw new Error('Cannot reach Ordering API. Please check if the service is running.');
  }
  
  return {
    token: tokenResponse.json('access_token'),
  };
}

export default function(data) {
  // Configuration
  const basketApiUrl = 'http://localhost:5223/api/v1';
  const orderingApiUrl = 'http://localhost:5133/api/v1';
  
  const userId = `user-${__VU}-${__ITER}`;
  
  // Create a basket with items
  const basketItems = [
    {
      id: "1",
      productId: 1,
      productName: "Test Product 1",
      unitPrice: 10.5,
      oldUnitPrice: 10.5,
      quantity: 2,
      pictureUrl: "http://example.com/product1.jpg"
    },
    {
      id: "2",
      productId: 2,
      productName: "Test Product 2",
      unitPrice: 15.75,
      oldUnitPrice: 15.75,
      quantity: 1,
      pictureUrl: "http://example.com/product2.jpg"
    }
  ];
  
  const basket = {
    buyerId: userId,
    items: basketItems
  };
  
  // Create/update the basket
  const createBasketResponse = http.post(`${basketApiUrl}/basket`, JSON.stringify(basket), {
    headers: {
      'Content-Type': 'application/json',
    },
  });
  
  const createBasketSuccess = check(createBasketResponse, {
    'create basket successful': (r) => r.status === 200 || r.status === 201,
  });
  
  failRate.add(!createBasketSuccess);
  
  if (createBasketSuccess) {
    // Create the order checkout object
    const orderCheckout = {
      city: "Test City",
      street: "Test Street",
      state: "Test State",
      country: "Test Country",
      zipCode: "12345",
      cardNumber: "4012888888881881",
      cardHolderName: "Test User",
      cardExpiration: "12/25",
      cardSecurityNumber: "123",
      cardTypeId: 1,
      buyer: userId
    };
    
    // Checkout the basket
    const startTime = new Date();
    const checkoutResponse = http.post(`${basketApiUrl}/basket/checkout`, JSON.stringify(orderCheckout), {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${data.token}`,
      },
    });
    const endTime = new Date();
    
    checkoutTrend.add(endTime - startTime);
    
    const checkoutSuccess = check(checkoutResponse, {
      'checkout successful': (r) => r.status === 202,
    });
    
    failRate.add(!checkoutSuccess);
    
    // If checkout was successful, verify that the basket is now empty
    if (checkoutSuccess) {
      sleep(2); // Give some time for the async processing
      
      const getBasketResponse = http.get(`${basketApiUrl}/basket/${userId}`);
      
      const basketEmptySuccess = check(getBasketResponse, {
        'basket should be empty after checkout': (r) => {
          if (r.status === 404) return true; // If basket was deleted, that's fine
          if (r.status === 200) {
            const basketObj = r.json();
            return basketObj.items.length === 0; // Or if it exists but is empty
          }
          return false;
        },
      });
      
      failRate.add(!basketEmptySuccess);
      
      // Try to get orders for the user
      // Note: In a real system, there might be a delay before the order is visible
      sleep(3); // Give more time for order processing
      
      const getOrdersResponse = http.get(`${orderingApiUrl}/orders?pageSize=10&pageIndex=0`, {
        headers: {
          'Authorization': `Bearer ${data.token}`,
        },
      });
      
      check(getOrdersResponse, {
        'get orders successful': (r) => r.status === 200,
        'orders are returned': (r) => r.status === 200 && r.json().data && r.json().data.length > 0,
      });
    }
  }
  
  // Wait between iterations
  sleep(1);
}