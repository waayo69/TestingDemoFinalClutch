using TestingDemo.Models;

namespace TestingDemo.ViewModels
{
    public class FinanceDashboardViewModel
    {
        public PaginatedList<ClientModel> PendingClients { get; set; }
        public PaginatedList<ClientModel> ClearanceClients { get; set; }
    }
}