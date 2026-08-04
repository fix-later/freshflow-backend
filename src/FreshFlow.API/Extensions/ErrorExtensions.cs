using FreshFlow.SharedKernel.Application;
using Microsoft.AspNetCore.Mvc;

namespace FreshFlow.API.Extensions;

public static class ErrorExtensions
{
    public static IActionResult ToActionResult(this Error error)
    {
        var body = Envelope(error.Code, error.Message);

        if (error.Code.EndsWith("_NOT_FOUND"))
            return new NotFoundObjectResult(body);

        if (error.Code is "EMAIL_ALREADY_EXISTS" or "PHONE_ALREADY_EXISTS"
                        or "REFRESH_TOKEN_REUSE" or "ALREADY_APPROVED"
                        or "CATEGORY_NAME_CONFLICT" or "UNIT_NAME_CONFLICT"
                        or "PACKING_CODE_CONFLICT"
                        or "CATEGORY_HAS_ACTIVE_CHILDREN"
                        or "PLATE_NUMBER_DUPLICATE"
                        or "VEHICLE_NOT_AVAILABLE"
                        or "MARKET_PRODUCT_ALREADY_EXISTS"
                        or "ALREADY_RECEIVED"
                        or "ROUTE_NOT_ASSIGNED"
                        or "ROUTE_NOT_REORDERABLE"
                        or "ROUTE_LOCKED_FOR_SORTING"
                        or "DELIVERY_ALREADY_EXISTS"
                        or "ROUTE_NOT_STARTABLE"
                        or "ROUTE_HAS_NO_DELIVERIES"
                        or "PENDING_HUB_DISCREPANCY"
                        or "DELIVERY_STATUS_INVALID"
                        or "ORDER_ALREADY_IN_ACTIVE_GROUP"
                        or "BATCH_NOT_MERGEABLE"
                        or "BATCH_NOT_MANIFESTABLE"
                        or "BATCH_NOT_MANIFESTED"
                        or "BATCH_ALREADY_HANDED_OFF"
                        or "BATCH_NOT_PURCHASED"
                        or "BATCH_ALREADY_IN_PROGRESS"
                        or "BATCH_NOT_CANCELLABLE"
                        or "BATCH_RESET_NOT_ALLOWED"
                        or "BATCH_CANCELLED"
                        or "HUB_ALREADY_CONFIGURED_FOR_MARKET")
            return new ConflictObjectResult(body);

        if (error.Code is "UNAUTHORIZED" or "INVALID_CREDENTIALS" or "INVALID_CURRENT_PASSWORD"
            or "TOKEN_INVALID" or "REFRESH_TOKEN_EXPIRED" or "REFRESH_TOKEN_REVOKED")
            return new UnauthorizedObjectResult(body);

        if (error.Code is "FORBIDDEN" or "MARKET_ACCESS_DENIED" or "HUB_ACCESS_DENIED")
            return new ObjectResult(body) { StatusCode = 403 };

        if (error.Code is "OPTIMISTIC_CONCURRENCY_CONFLICT" or "SERIALIZATION_CONFLICT"
                          or "STOCK_RESERVATION_CONFLICT")
            return new ConflictObjectResult(body);

        if (error.Code is "VALIDATION_ERROR" or "INVALID_ROLE" or "WEAK_PASSWORD")
            return new BadRequestObjectResult(body);

        if (error.Code is "ACCOUNT_LOCKED")
            return new ObjectResult(body) { StatusCode = 423 };

        if (error.Code is "RESET_TOKEN_INVALID" or "RESET_TOKEN_EXPIRED" or "OTP_INVALID")
            return new BadRequestObjectResult(body);

        if (error.Code is "CHANNEL_NOT_SUPPORTED" or "CANNOT_DEACTIVATE_SELF" or "INVALID_MARKET"
                        or "INVALID_ASSIGNMENT_TARGET" or "INVALID_UNIT" or "INVALID_CATEGORY"
                        or "INVALID_PACKING_CODE"
                        or "INVALID_CATEGORY_PARENT"
                        or "INVALID_PRICE" or "INVALID_QUANTITY"
                        or "RESTAURANT_NOT_APPROVED" or "INVALID_PRODUCT" or "INSUFFICIENT_STOCK"
                        or "MINIMUM_ORDER_QUANTITY_NOT_MET" or "DELIVERY_COORDINATES_REQUIRED"
                        or "INVALID_DELIVERY_FEE" or "VAT_RATE_MISSING"
                        or "ORDER_EMPTY" or "INVALID_AMOUNT" or "CREDIT_LIMIT_EXCEEDED"
                        or "INVALID_CLAIM_AMOUNT" or "INVALID_CLAIM_DECISION_NOTE"
                        or "CREDIT_REFUND_EXCEEDS_ORDER_CHARGE"
                        or "CREDIT_SETTLEMENT_EXCEEDS_BALANCE" or "CREDIT_REFUND_EXCEEDS_BALANCE"
                        or "INVALID_CREDIT_LIMIT" or "CREDIT_LIMIT_BELOW_OUTSTANDING_BALANCE"
                        or "DELIVERY_DATE_OUT_OF_WINDOW" or "INVALID_ACTUAL_QUANTITY"
                        or "SCHEDULED_ORDER_FIRST_RUN_IN_PAST" or "INVALID_ISSUE_QUANTITY"
                        or "HUB_RELAY_NOT_SUPPORTED" or "STOP_LIMIT_EXCEEDED"
                        or "MISSING_COORDINATES" or "INVALID_STOP_ORDER"
                        or "VEHICLE_NOT_ELIGIBLE" or "FLEET_CAPACITY_EXCEEDED"
                        or "HUB_CAPACITY_EXCEEDED"
                        or "HUB_CAPACITY_BELOW_OCCUPIED"
                        or "INSUFFICIENT_HUB_STOCK" or "INBOUND_NOT_ARRIVED"
                        or "ORDER_ITEM_NOT_IN_INBOUND"
                        or "OUTBOUND_ROUTE_INVALID" or "ROUTE_HAS_NO_DRIVER"
                        or "DRIVER_REQUIRED" or "DRIVER_ROUTE_MISMATCH"
                        or "ROUTE_HUB_MISMATCH" or "PICKUP_ORDERS_INCOMPLETE"
                        or "ORDER_NOT_AT_HUB"
                        or "ORDER_NOT_ON_ROUTE" or "MARKET_PRODUCT_NOT_FOUND"
                        or "INVALID_PROCUREMENT_BATCH"
                        or "REFERENCE_PRICE_MISSING"
                        or "INVALID_AGENT"
                        or "AGENT_NOT_ELIGIBLE"
                        or "PURCHASE_LINES_MISMATCH"
                        or "INVALID_PURCHASE_LINE"
                        or "MARKET_INACTIVE"
                        or "HUB_INACTIVE"
                        or "HUB_NOT_CONFIGURED_FOR_MARKET"
                        or "BUYER_TAX_CODE_REQUIRED" or "BUYER_TAX_CODE_INVALID"
                        or "BUYER_LEGAL_NAME_REQUIRED" or "BUYER_ADDRESS_REQUIRED"
                        or "INVOICE_LINE_UNIT_REQUIRED" or "INVOICE_NOT_ISSUED" or "INVOICE_EXPORT_INCOMPLETE"
            || error.Code.StartsWith("ACCOUNT_"))
            return new UnprocessableEntityObjectResult(body);

        if (error.Code is "ORDER_NOT_DRAFT" or "ORDER_CANNOT_CANCEL" or "ORDER_INVALID_TRANSITION"
                        or "ORDER_CANNOT_RESCHEDULE" or "ORDER_NOT_CANCELLABLE" or "ORDER_CANNOT_ADJUST"
                        or "ORDER_NOT_DELIVERED" or "ORDER_RECEIPT_ALREADY_CONFIRMED"
                        or "CLAIM_ORDER_NOT_CLAIMABLE" or "CLAIM_INVALID_TRANSITION"
                        or "ORDER_ISSUE_NOT_ALLOWED" or "ORDER_ISSUE_ALREADY_RESOLVED"
                        or "SCHEDULED_ORDER_NOT_ACTIVE" or "SCHEDULED_ORDER_ALREADY_CANCELLED"
                        or "ROUTE_INVALID_TRANSITION" or "HUB_HAS_PENDING_DELIVERIES"
                        or "HUB_HAS_ACTIVE_PROCUREMENT"
                        or "DISCREPANCY_ALREADY_ACKNOWLEDGED"
                        or "HUB_HANDOVER_ALREADY_CHECKED_OUT")
            return new ConflictObjectResult(body);

        if (error.Code is "ROLE_NOT_CONFIGURED")
            return new ObjectResult(body) { StatusCode = 500 };

        if (error.Code is "SCAN_NO_MATCH")
            return new NotFoundObjectResult(body);

        return new ObjectResult(body) { StatusCode = 500 };
    }

    private static object Envelope(string code, string message) =>
        new { success = false, error = new { code, message } };
}
