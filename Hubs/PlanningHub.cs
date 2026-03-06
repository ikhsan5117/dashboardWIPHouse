using Microsoft.AspNetCore.SignalR;

namespace dashboardWIPHouse.Hubs
{
    /// <summary>
    /// SignalR Hub untuk real-time update Supply Finishing Dashboard.
    /// Client terhubung ke /planningHub dan menerima event "planningUpdated"
    /// setiap kali ada transaksi After Washing OUT baru.
    /// </summary>
    public class PlanningHub : Hub
    {
        // Hub ini hanya digunakan sebagai push channel.
        // Logika data tetap di AfterWashingController (GetElwpPlanningData).
    }
}
