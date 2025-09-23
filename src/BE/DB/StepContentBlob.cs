using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentBlob")]
public partial class StepContentBlob
{
    [Key]
    public long Id { get; set; }

    public byte[] Content { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("StepContentBlob")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
