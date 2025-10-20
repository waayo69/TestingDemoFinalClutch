using TestingDemo.Models;

namespace TestingDemo.ViewModels
{
    public class CustomerCareDashboardViewModel
    {
        public PaginatedList<ClientModel> LiaisonClients { get; set; }
        public string CurrentSort { get; set; }
        public string NameSortParm { get; set; }
        public string DateSortParm { get; set; }
    }
}