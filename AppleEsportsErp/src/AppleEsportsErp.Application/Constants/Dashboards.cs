namespace AppleEsportsErp.Application.Constants;

/// <summary>SOP §19: Dashboards that can be permission-controlled per operator</summary>
public static class Dashboards
{
    public const string BillingCounter = "billing_counter";
    public const string Sessions = "sessions";
    public const string Reservations = "reservations";
    public const string FoodOrders = "food_orders";
    public const string CashRegister = "cash_register";
    public const string CashDesk = "cash_desk";
    public const string Members = "members";
    public const string MenuEditor = "menu_editor";
    public const string MainDashboard = "main_dashboard";
    public const string PcStatus = "pc_status";
    public const string Eod = "eod";
    public const string Settings = "settings";
    public const string WalletSettings = "wallet_settings";
    public const string MemberValueEdit = "member_value_edit";

    /// <summary>
    /// Never registered as an actual policy - `[Authorize(Policy = "Dashboard:reports")]`
    /// referenced this name directly as a string, with no constant and nothing in
    /// Program.cs's policy loop to back it. Every request to that endpoint threw
    /// "AuthorizationPolicy named: 'Dashboard:reports' was not found" - a 500, on any
    /// server running this code, the first time anyone actually opened Reports.
    /// </summary>
    public const string Reports = "reports";

    /// <summary>
    /// Deliberately not in <see cref="AdminOnly"/>. An operator is the person sitting at the
    /// branch when an update lands, so they are the one who needs to see what it contains and
    /// whether it installed. Hiding it from them would mean the only people who can see the
    /// state of a branch are the ones not standing in it.
    /// </summary>
    public const string Updates = "updates";

    /// <summary>SOP §19: Super Admin-only dashboards</summary>
    public static readonly string[] AdminOnly = { PcStatus, Settings, WalletSettings, MemberValueEdit };
}
