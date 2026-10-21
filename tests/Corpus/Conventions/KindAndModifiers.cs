// mustBe needs every trait, mustNotBe forbids each. 'areNot' leaves the interface itself out.
using App.Domain;

namespace App.Domain.Events
{
    public interface IOrderEvent : IDomainEvent { }

    public sealed record OrderPlaced : IOrderEvent;

    public record OrderShipped : IDomainEvent; // ARCH105 T:App.Domain.Events.OrderShipped mustBe=sealed,record "OrderShipped"

    public sealed class OrderCancelled : IDomainEvent { } // ARCH105 T:App.Domain.Events.OrderCancelled mustBe=sealed,record "OrderCancelled"

    public readonly record struct OrderPaid : IDomainEvent; // ARCH105 T:App.Domain.Events.OrderPaid mustBe=sealed,record "OrderPaid"
}

namespace App.Infrastructure.Mail
{
    internal sealed class SmtpSender { }

    public sealed class PublicSender { } // ARCH105 T:App.Infrastructure.Mail.PublicSender mustNotBe=public "PublicSender"

    public interface ISender { }

    public static class Extensions { } // ARCH105 T:App.Infrastructure.Mail.Extensions mustNotBe=public "Extensions"
}
