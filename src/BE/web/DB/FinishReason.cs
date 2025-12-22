using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("FinishReason")]
public partial class FinishReason
{
    [Key]
    public byte Id { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string Name { get; set; } = null!;

    [InverseProperty("FinishReason")]
    public virtual ICollection<UserModelUsage> UserModelUsages { get; set; } = new List<UserModelUsage>();
}
