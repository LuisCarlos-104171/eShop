# Load Testing with K6

This directory contains K6 load test scripts for the eShop application. The tests are designed to measure the performance of different services in the application.

## Test Scripts

- `loadtest.js` - Tests the Identity service authentication flow
- `catalog-test.js` - Tests browsing the catalog, including fetching items, details, brands, and types
- `basket-test.js` - Tests basket operations including creating, updating, and retrieving baskets
- `checkout-test.js` - Tests the entire checkout flow from creating a basket to placing an order

## Running Tests

Make sure the eShop application is running before executing the tests. You can run the tests using either K6 directly or npm scripts.

### Using K6 directly

```bash
# Run the identity service test
k6 run loadtest/loadtest.js

# Run the catalog service test
k6 run loadtest/catalog-test.js

# Run the basket service test
k6 run loadtest/basket-test.js

# Run the checkout flow test
k6 run loadtest/checkout-test.js

# Run a shorter test for quick validation
k6 run loadtest/catalog-test.js --iterations=10 --vus=2
```

### Using npm scripts

```bash
# Run the identity service test
npm run loadtest

# Run the catalog service test
npm run loadtest:catalog

# Run the basket service test
npm run loadtest:basket

# Run the checkout flow test
npm run loadtest:checkout

# Run all tests sequentially
npm run loadtest:all
```

## Environment Configuration

The load tests are configured to connect to the eShop services on localhost. If you're running the services on different hosts or ports, you'll need to update the URLs in the test scripts:

- Identity Service: https://localhost:5243
- Catalog Service: http://localhost:5133
- Basket Service: http://localhost:5223
- Ordering Service: http://localhost:5133

## Test Scenarios

Each test script includes a scenario configuration that defines:

1. The number of virtual users (VUs)
2. The duration of the test
3. The ramp-up and ramp-down periods

For example, the catalog test ramps up from 1 to 10 users, stays at 10 users for 1 minute, then ramps up to 50 users for another minute before ramping down.

## Performance Thresholds

Each test defines performance thresholds that should be met for the test to be considered successful:

- HTTP request duration (response time)
- Failed requests rate
- Custom metrics specific to each service (e.g., checkout time)

## Integration with CI/CD

To integrate these tests with your CI/CD pipeline, you can:

1. Run the tests as part of your deployment process
2. Export the results to a format that can be analyzed (JSON, CSV)
3. Compare results against baseline measurements
4. Fail the build if performance degrades beyond acceptable thresholds

## Visualizing Results

K6 results can be visualized using:

1. Built-in K6 output
2. Grafana dashboards (eShop already has Grafana set up)
3. K6 Cloud for more detailed analysis

For more information on K6, visit https://k6.io/docs/
