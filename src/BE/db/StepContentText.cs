using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("StepContentText")]
public partial class StepContentText
{
    [Key]
    public long Id { get; set; }

    public string Content { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("StepContentText")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
