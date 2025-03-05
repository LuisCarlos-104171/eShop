import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

// Custom metrics
const failRate = new Rate('failed_requests');

// Test configuration
export const options = {
  // Test scenarios
  scenarios: {
    login_performance: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 5 },   // Ramp up to 5 users
        { duration: '1m', target: 5 },    // Stay at 5 users for 1 minute
        { duration: '30s', target: 20 },  // Ramp up to 20 users
        { duration: '1m', target: 20 },   // Stay at 20 users for 1 minute
        { duration: '30s', target: 0 },   // Ramp down to 0 users
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    // Define performance thresholds
    http_req_duration: ['p(95)<500'],    // 95% of requests should complete within 500ms
    'failed_requests': ['rate<0.1'],     // Less than 10% of requests should fail
  },
  // Disable certificate validation for development/testing
  insecureSkipTLSVerify: true,
};

// Main function executed for each virtual user
export default function() {
  // Configuration
  const identityServerUrl = 'https://localhost:5243'; // Replace with your actual URL
  const clientId = 'loadtest';             // Using existing client from config
  const clientSecret = 'secret';         // From your configuration

  // User credentials - do not hardcode in production!
  const username = 'alice';
  const password = 'Pass123$';

  // Request to get token
  // Using password grant type which is now properly configured for the loadtest client
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

  // Make the authentication request
  const response = http.post(tokenUrl, payload, params);

  // Check if the request was successful
  const success = check(response, {
    'status is 200': (r) => r.status === 200,
    'has access token': (r) => r.json('access_token') !== undefined,
  });

  // Add to our custom metric
  failRate.add(!success);

  // If successful, we could make additional authenticated requests here
  if (success) {
    // Example of using the token (uncomment and customize if needed)
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

  // Wait between iterations
  sleep(1);
}

// Add a setup function to validate connection before the test
export function setup() {
  const identityServerUrl = 'https://localhost:5243'; // Replace with your actual URL
  const res = http.get(`${identityServerUrl}/.well-known/openid-configuration`);

  const success = check(res, {
    'Identity Server is reachable': (r) => r.status === 200,
  });

  if (!success) {
    throw new Error('Cannot reach Identity Server. Please check the URL and try again.');
  }

  return {};
}
