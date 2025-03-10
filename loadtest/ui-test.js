import http from "k6/http";
import { sleep, check } from "k6";
import { Rate, Trend } from "k6/metrics";
import { htmlReport } from "https://raw.githubusercontent.com/benc-uk/k6-reporter/main/dist/bundle.js";

// Custom metrics
const successRate = new Rate("successful_requests");
const landingPageLoadTime = new Trend("landing_page_load_time");

// Configuration
export const options = {
    // Test scenario
    scenarios: {
        landing_page: {
            executor: "ramping-vus",
            startVUs: 1,
            stages: [
                { duration: "10s", target: 100 }, // Ramp up to 100 users over 10 seconds
                { duration: "30s", target: 500 }, // Ramp up to 500 users over 30 seconds
                { duration: "30s", target: 2000 }, // Ramp up to 2000 users over 30 seconds
                { duration: "1m", target: 2000 }, // Stay at 2000 users for 1 minute
                { duration: "20s", target: 0 }, // Ramp down to 0 users
            ],
            gracefulRampDown: "5s",
        },
    },
    thresholds: {
        successful_requests: ["rate>0.95"], // 95% of requests should succeed
        http_req_duration: ["p(95)<1500"], // 95% of requests should be below 1.5s
        landing_page_load_time: ["avg<1000"], // Average load time should be below 1s
    },
    insecureSkipTLSVerify: true,
};

// This function will be executed for each virtual user
export default function () {
    const baseUrl = __ENV.BASE_URL || "https://localhost:7298";

    // Hit only the landing page
    let response = http.get(`${baseUrl}/`, {
        tags: { page: "landing" },
    });

    // Check if the landing page loaded successfully
    check(response, {
        "landing page status is 200": (r) => r.status === 200,
        "landing page contains expected content": (r) =>
            r.body.includes("eShop"),
    });

    // Record metrics
    landingPageLoadTime.add(response.timings.duration);
    successRate.add(response.status === 200);

    // Add a small random delay between requests to simulate real user behavior
    sleep(Math.random() * 2 + 1); // Random sleep 1-3 seconds
}
