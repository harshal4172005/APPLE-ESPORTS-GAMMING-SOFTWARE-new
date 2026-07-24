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

    /// <summary>SOP §19: Super Admin-only dashboards</summary>
    public static readonly string[] AdminOnly = { PcStatus, Settings, WalletSettings, MemberValueEdit };
}
