using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentThink")]
public partial class StepContentThink
{
    [Key]
    public long Id { get; set; }

    public string Content { get; set; } = null!;

    public byte[]? Signature { get; set; }

    [ForeignKey("Id")]
    [InverseProperty("StepContentThink")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
