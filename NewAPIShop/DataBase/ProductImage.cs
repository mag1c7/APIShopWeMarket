using System;
using System.Collections.Generic;

namespace NewAPIShop.DataBase;

public partial class ProductImage
{
    public int ImageId { get; set; }

    public int ProductId { get; set; }

    public byte[] ImageProduct { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
