using System.Collections.Generic;
using TestingDemo.Models;

namespace TestingDemo.Models.ViewModels
{
    public class PlanningDashboardViewModel
    {
        public PaginatedList<ClientModel> PendingClients { get; set; }
        public PaginatedList<ClientModel> CompletedClients { get; set; }
        
        // Dictionary to store requirements for each client for easy access in view
        public IDictionary<int, List<PermitRequirementModel>> RequirementsByClient { get; set; }
    }
}
