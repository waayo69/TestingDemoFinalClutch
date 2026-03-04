using System.Collections.Generic;
using TestingDemo.Models;

namespace TestingDemo.ViewModels
{
    public class DashboardViewModel
    {
        public List<ClientQueueItem> LiaisonClients { get; set; } = new();
        public List<ClientQueueItem> FinanceClients { get; set; } = new();
        public List<ClientQueueItem> PlanningClients { get; set; } = new();
        public List<ClientQueueItem> ReceivedClients { get; set; } = new();
        public List<ClientQueueItem> DocumentationClients { get; set; } = new();
        public List<ClientQueueItem> ClearanceClients { get; set; } = new();
    }

    public class ClientQueueItem
    {
        public ClientModel Client { get; set; }
        public string? AssignedUserName { get; set; }
    }
}