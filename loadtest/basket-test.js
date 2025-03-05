import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const failRate = new Rate('failed_requests');
const basketOperationTrend = new Trend('basket_operation_time');

export const options = {
  scenarios: {
    basket_operations: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 },   // Ramp up to 10 users
        { duration: '1m', target: 10 },    // Stay at 10 users for 1 minute
        { duration: '30s', target: 40 },   // Ramp up to 40 users
        { duration: '1m', target: 40 },    // Stay at 40 users for 1 minute
        { duration: '30s', target: 0 },    // Ramp down to 0 users
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_duration: ['p(95)<250'],      // 95% of requests should complete within 250ms
    'failed_requests': ['rate<0.05'],      // Less than 5% of requests should fail
    'basket_operation_time': ['p(95)<300'], // 95% of basket operations under 300ms
  },
  // Disable certificate validation for development/testing
  insecureSkipTLSVerify: true,
};

export default function() {
  // Configuration
  const basketApiUrl = 'http://localhost:5223/api/v1';
  const userId = `user-${__VU}-${__ITER}`;
  
  // Create a new, empty basket
  const emptyBasket = {
    buyerId: userId,
    items: []
  };
  
  // First, try to get the basket (which likely doesn't exist yet)
  const getResponse = http.get(`${basketApiUrl}/basket/${userId}`);
  
  // Update or create basket with initial items
  const basketItems = [
    {
      id: "1",
      productId: 1,
      productName: "Product 1",
      unitPrice: 10,
      oldUnitPrice: 10,
      quantity: 1,
      pictureUrl: "http://example.com/product1.jpg"
    },
    {
      id: "2",
      productId: 2,
      productName: "Product 2",
      unitPrice: 15,
      oldUnitPrice: 15,
      quantity: 2,
      pictureUrl: "http://example.com/product2.jpg"
    }
  ];
  
  const basketWithItems = {
    buyerId: userId,
    items: basketItems
  };
  
  // Create/update the basket
  const startTime = new Date();
  const updateResponse = http.post(`${basketApiUrl}/basket`, JSON.stringify(basketWithItems), {
    headers: { 'Content-Type': 'application/json' }
  });
  const endTime = new Date();
  
  // Add to our custom metric
  basketOperationTrend.add(endTime - startTime);
  
  // Check if the basket update was successful
  const updateSuccess = check(updateResponse, {
    'basket update status is 200': (r) => r.status === 200 || r.status === 201,
  });
  
  failRate.add(!updateSuccess);
  
  if (updateSuccess) {
    // Get the updated basket
    const getUpdatedResponse = http.get(`${basketApiUrl}/basket/${userId}`);
    
    // Check if getting the updated basket was successful
    const getUpdatedSuccess = check(getUpdatedResponse, {
      'get updated basket status is 200': (r) => r.status === 200,
      'updated basket has correct items': (r) => {
        const basket = r.json();
        return basket && basket.items && basket.items.length === 2;
      }
    });
    
    failRate.add(!getUpdatedSuccess);
    
    // Add another item to the basket
    const additionalItem = {
      id: "3",
      productId: 3,
      productName: "Product 3",
      unitPrice: 20,
      oldUnitPrice: 20,
      quantity: 1,
      pictureUrl: "http://example.com/product3.jpg"
    };
    
    basketItems.push(additionalItem);
    
    const basketWithAdditionalItem = {
      buyerId: userId,
      items: basketItems
    };
    
    // Update the basket with the new item
    const updateWithNewItemStartTime = new Date();
    const updateWithNewItemResponse = http.post(`${basketApiUrl}/basket`, JSON.stringify(basketWithAdditionalItem), {
      headers: { 'Content-Type': 'application/json' }
    });
    const updateWithNewItemEndTime = new Date();
    
    // Add to our custom metric
    basketOperationTrend.add(updateWithNewItemEndTime - updateWithNewItemStartTime);
    
    // Check if the basket update with the new item was successful
    const updateWithNewItemSuccess = check(updateWithNewItemResponse, {
      'basket update with new item status is 200': (r) => r.status === 200 || r.status === 201,
    });
    
    failRate.add(!updateWithNewItemSuccess);
    
    // Sometimes remove an item (to simulate real user behavior)
    if (Math.random() > 0.5) {
      // Remove the first item
      basketItems.splice(0, 1);
      
      const basketWithRemovedItem = {
        buyerId: userId,
        items: basketItems
      };
      
      // Update the basket with the removed item
      const removeItemStartTime = new Date();
      const removeItemResponse = http.post(`${basketApiUrl}/basket`, JSON.stringify(basketWithRemovedItem), {
        headers: { 'Content-Type': 'application/json' }
      });
      const removeItemEndTime = new Date();
      
      // Add to our custom metric
      basketOperationTrend.add(removeItemEndTime - removeItemStartTime);
      
      // Check if the basket update with the removed item was successful
      const removeItemSuccess = check(removeItemResponse, {
        'basket update with removed item status is 200': (r) => r.status === 200 || r.status === 201,
      });
      
      failRate.add(!removeItemSuccess);
    }
    
    // Get the final basket
    const getFinalResponse = http.get(`${basketApiUrl}/basket/${userId}`);
    
    // Check if getting the final basket was successful
    const getFinalSuccess = check(getFinalResponse, {
      'get final basket status is 200': (r) => r.status === 200,
    });
    
    failRate.add(!getFinalSuccess);
  }
  
  // Wait between iterations
  sleep(1);
}

// Setup function to validate connection before the test
export function setup() {
  const basketApiUrl = 'http://localhost:5223/api/v1';
  const testUserId = 'test-user-setup';
  
  // Try to get a basket to verify the API is reachable
  const res = http.get(`${basketApiUrl}/basket/${testUserId}`);
  
  const success = check(res, {
    'Basket API is reachable': (r) => r.status === 200 || r.status === 404, // 404 is acceptable since the basket may not exist
  });
  
  if (!success) {
    throw new Error('Cannot reach Basket API. Please check if the service is running.');
  }
  
  return {};
}