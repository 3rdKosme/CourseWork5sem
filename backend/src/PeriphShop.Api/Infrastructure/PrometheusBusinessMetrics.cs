using PeriphShop.Application.Abstractions;
using PeriphShop.Domain.Enums;
using Prometheus;

namespace PeriphShop.Api.Infrastructure;

/// <summary>
/// Бизнес-метрики магазина в формате Prometheus (раздел 10.1 ТЗ).
/// Именование: префикс periphshop_, суффикс _total для счётчиков.
/// </summary>
public class PrometheusBusinessMetrics : IBusinessMetrics
{
    private static readonly Counter OrdersCreated = Metrics.CreateCounter(
        "periphshop_orders_created_total", "Количество созданных заказов");

    private static readonly Counter OrderRevenue = Metrics.CreateCounter(
        "periphshop_order_revenue_rub_total", "Накопленная сумма созданных заказов, руб.");

    private static readonly Counter StatusTransitions = Metrics.CreateCounter(
        "periphshop_order_status_transitions_total", "Переходы статусов заказов",
        new CounterConfiguration { LabelNames = ["from", "to"] });

    private static readonly Counter CartItemsAdded = Metrics.CreateCounter(
        "periphshop_cart_items_added_total", "Количество добавленных в корзину единиц товара");

    private static readonly Counter CheckoutFailures = Metrics.CreateCounter(
        "periphshop_checkout_failures_total", "Отказы оформления заказа",
        new CounterConfiguration { LabelNames = ["reason"] });

    private static readonly Counter LoginAttempts = Metrics.CreateCounter(
        "periphshop_login_attempts_total", "Попытки входа в систему",
        new CounterConfiguration { LabelNames = ["result"] });

    private static readonly Gauge ActiveProducts = Metrics.CreateGauge(
        "periphshop_products_total", "Количество активных товаров в каталоге");

    private static readonly Gauge LowStockProducts = Metrics.CreateGauge(
        "periphshop_products_low_stock", "Количество товаров с остатком ниже порога");

    public void OrderCreated(decimal total)
    {
        OrdersCreated.Inc();
        OrderRevenue.Inc((double)total);
    }

    public void OrderStatusChanged(OrderStatus from, OrderStatus to) =>
        StatusTransitions.WithLabels(from.ToString(), to.ToString()).Inc();

    public void CartItemAdded(int quantity) => CartItemsAdded.Inc(quantity);

    public void CheckoutFailed(string reason) => CheckoutFailures.WithLabels(reason).Inc();

    public void LoginAttempt(bool success) => LoginAttempts.WithLabels(success ? "success" : "failure").Inc();

    public void SetCatalogGauges(int activeProducts, int lowStockProducts)
    {
        ActiveProducts.Set(activeProducts);
        LowStockProducts.Set(lowStockProducts);
    }
}
