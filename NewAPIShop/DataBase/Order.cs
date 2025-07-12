using System;
using System.Collections.Generic;

namespace NewAPIShop.DataBase;

public partial class Order
{
    public int OrderId { get; set; }

    public int UserId { get; set; }

    public DateTime OrderDate { get; set; }

    public decimal Total { get; set; }

    public string PaymentStatus { get; set; } = null!;

    public DateTime? PickupDate { get; set; }

    public bool IsPickup { get; set; }

    /// <summary>
    /// ID пункта выдачи
    /// </summary>
    public int? PickupPointId { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual PickupPoint? PickupPoint { get; set; }

    public virtual User User { get; set; } = null!;
}
