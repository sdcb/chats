using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ModelProviderOrder")]
[Index("Order", Name = "IX_ModelProviderOrder_Order")]
public partial class ModelProviderOrder
{
    [Key]
    public short ModelProviderId { get; set; }

    public short Order { get; set; }
}
