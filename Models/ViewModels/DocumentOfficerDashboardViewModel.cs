using TestingDemo.Models;

namespace TestingDemo.ViewModels
{
    public class DocumentOfficerDashboardViewModel
    {
        public PaginatedList<ClientModel> PendingClients { get; set; }
        public PaginatedList<ClientModel> ArchivedClients { get; set; }
    }
}
