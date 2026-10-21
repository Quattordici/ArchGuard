// The list form: each layer may depend on the layers after it, never on the ones before it.
#nullable enable

namespace App.Domain.Cases
{
    public class UsesApplication
    {
        public App.Application.OrderService? Service; // ARCH001 F:App.Domain.Cases.UsesApplication.Service T:App.Application.OrderService "OrderService"
    }
}

namespace App.Application.Cases
{
    public class UsesInfrastructure
    {
        public App.Infrastructure.Db? Db; // ARCH001 F:App.Application.Cases.UsesInfrastructure.Db T:App.Infrastructure.Db "Db"

        public App.Api.Controller? Controller; // ARCH001 F:App.Application.Cases.UsesInfrastructure.Controller T:App.Api.Controller "Controller"
    }
}

namespace App.Infrastructure.Cases
{
    public class UsesApi
    {
        public App.Api.Controller? Controller; // ARCH001 F:App.Infrastructure.Cases.UsesApi.Controller T:App.Api.Controller "Controller"
    }
}
