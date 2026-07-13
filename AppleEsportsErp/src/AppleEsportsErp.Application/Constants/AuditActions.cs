namespace AppleEsportsErp.Application.Constants;

/// <summary>SOP §22: Audit Action Types — every critical action must be logged</summary>
public static class AuditActions
{
    // Auth
    public const string Login = "login";
    public const string Logout = "logout";
    public const string FailedLogin = "failed_login";
    public const string ForcedLogout = "forced_logout";
    public const string AdminSwitchIn = "admin_switch_in";
    public const string AdminSwitchOut = "admin_switch_out";

    // Sessions
    public const string SessionStart = "session_start";
    public const string SessionStop = "session_stop";
    public const string SessionExtend = "session_extend";
    public const string SessionTransfer = "session_transfer";

    // Reservations
    public const string ReservationCreate = "reservation_create";
    public const string ReservationCancel = "reservation_cancel";
    public const string ReservationOverride = "reservation_override";
    public const string ReservationExpire = "reservation_expire";

    // Billing
    public const string BillCreate = "bill_create";
    public const string BillComplete = "bill_complete";
    public const string PaymentProcess = "payment_process";
    public const string DiscountApply = "discount_apply";

    // Cash
    public const string CashOpening = "cash_opening";
    public const string CashVerification = "cash_verification";
    public const string CashMismatch = "cash_mismatch";
    public const string DenominationCount = "denomination_count";

    // Members
    public const string MemberCreate = "member_create";
    public const string WalletRecharge = "wallet_recharge";
    public const string WalletDeduction = "wallet_deduction";
    public const string PointsRedeem = "points_redeem";

    // Operators
    public const string OperatorCreate = "operator_create";
    public const string OperatorRemove = "operator_remove";
    public const string OperatorSuspend = "operator_suspend";
    public const string AccessGrant = "access_grant";
    public const string AccessRevoke = "access_revoke";

    // Inventory
    public const string StockRefill = "stock_refill";
    public const string PriceChange = "price_change";
    public const string ItemDisable = "item_disable";
    public const string WastageLog = "wastage_log";

    // System
    public const string ShiftStart = "shift_start";
    public const string ShiftEnd = "shift_end";
    public const string EodFinalize = "eod_finalize";
    public const string ForceClose = "force_close";
    public const string SettingsChange = "settings_change";
}
