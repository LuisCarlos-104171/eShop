# eShop Checkout Process - Sequence Diagram Explanation

![Checkout Sequence Diagram](/docs/assets/seqdiag.png)

## Diagram Overview

The checkout sequence diagram illustrates the complete flow of user checkout in the eShop application, spanning from the initial user interaction to the final order confirmation. The diagram provides a comprehensive visualization of how multiple microservices interact during the checkout process.

## Participants

The diagram shows interactions between the following components:
- **User**: The customer using the eShop application
- **WebApp**: The frontend service handling user interface and initial request processing
- **BasketAPI**: Responsible for managing the user's shopping items
- **OrderingAPI**: Handles order creation and management
- **OrderProcessor**: Manages order workflow and state transitions
- **PaymentProcessor**: Handles payment processing
- **OpenTelemetry Collector**: Captures monitoring data throughout the process

## Checkout Flow Sequence

### 1. Initial User Actions & Form Validation
- The sequence begins with the user reviewing their cart and submitting an order
- The WebApp creates a trace labeled "InitiateCheckout" to mark the beginning of the process
- The form is validated locally within WebApp
- A span called "ValidateCart" is started for data validation

### 2. Basket Processing
- WebApp requests the basket items from BasketAPI ("Get Basket Items")
- BasketAPI returns the items to WebApp
- WebApp creates a "SubmitOrder" span to track the order submission process

### 3. Order Creation
- WebApp calls OrderingAPI via OrderingService
- The diagram shows a security measure where sensitive data (card numbers, addresses) are masked
- OrderingAPI creates an Order Command internally
- The order is saved with "Submitted" status
- A "Delete Basket" event is sent to BasketAPI to clear the user's cart
- WebApp receives confirmation that the order was created
- The user is redirected to their order history page

### 4. Background Processing Begins
- After the user is redirected, background processing continues
- OrderProcessor begins Grace Period Management
- An Order Creation Event is published 
- After the grace period expires (marked as "After Grace Period" in the diagram)
- The order is confirmed by the OrderProcessor

### 5. Stock Validation & Payment
- The OrderProcessor confirms stock availability 
- A Stock Confirmed Event is sent to PaymentProcessor
- PaymentProcessor processes the payment
- Upon successful payment, a Payment Success Event is published
- OrderingAPI updates the order status to "Paid"
- Order status is updated and visible to the user in their order history

## Distributed Tracing Features

The diagram highlights several OpenTelemetry tracing features:
- **Spans**: Multiple spans are created for different operations ("ValidateCart", "SubmitOrder")
- **Traces**: A full trace from checkout initiation to completion is maintained
- **Security**: The diagram shows "Mask PII" for sensitive data like card numbers and addresses
- **Events**: Event-driven communication between services is captured in the tracing

## Key Timing Aspects

The diagram effectively illustrates the asynchronous nature of the checkout process:
- The user-facing operations (until redirect to order history) happen quickly
- Background processing (grace period, stock validation, payment) continues after the user interface has moved on
- The entire process is traced from beginning to end with appropriate spans

This sequence diagram serves as an essential reference for understanding the complete checkout flow and would be valuable for developers implementing OpenTelemetry tracing across this process.
