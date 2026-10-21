// mustBeNamed and mustNotBeNamed; the selector's 'inherit' covers interfaces, and the arity in IRequestHandler<,> counts.
using App.Application;

namespace App.Application.Orders;

public sealed class PlaceOrderHandler : IRequestHandler<string, int> { }

public sealed class PlaceOrderProcessor : IRequestHandler<string, int> { } // ARCH101 T:App.Application.Orders.PlaceOrderProcessor mustBeNamed=*Handler "PlaceOrderProcessor"

public sealed class OneArgument : IRequestHandler<string> { }

public abstract class HandlerBase : IRequestHandler<string, int> { } // ARCH101 T:App.Application.Orders.HandlerBase mustBeNamed=*Handler "HandlerBase"

public sealed class Inherited : HandlerBase { } // ARCH101 T:App.Application.Orders.Inherited mustBeNamed=*Handler "Inherited"

public static class OrderManager { } // ARCH101 T:App.Application.Orders.OrderManager mustNotBeNamed=*Manager,*Helper "OrderManager"

public static class StringHelper { } // ARCH101 T:App.Application.Orders.StringHelper mustNotBeNamed=*Manager,*Helper "StringHelper"

public static class HelperFunctions { }

public class Outer
{
    public class CacheManager { } // ARCH101 T:App.Application.Orders.Outer.CacheManager mustNotBeNamed=*Manager,*Helper "CacheManager"
}

public partial class Split { } // ARCH101 T:App.Application.Orders.Split mustBeNamed=*Handler "Split"

public partial class Split : IRequestHandler<int, int> { } // reported once, on the first part
