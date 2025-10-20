using System.Collections.Generic;
using TestingDemo.Models;

namespace TestingDemo.ViewModels
{
    public class DashboardViewModel
    {
        public List<ClientModel> LiaisonClients { get; set; }
        public List<ClientModel> FinanceClients { get; set; }
        public List<ClientModel> PlanningClients { get; set; }
        public List<ClientModel> ReceivedClients { get; set; }
        public List<ClientModel> DocumentationClients { get; set; }
    }
}