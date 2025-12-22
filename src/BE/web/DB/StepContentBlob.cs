using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("StepContentBlob")]
public partial class StepContentBlob
{
    [Key]
    public long Id { get; set; }

    public byte[] Content { get; set; } = null!;

    [StringLength(100)]
    public string MediaType { get; set; } = null!;

    [StringLength(200)]
    public string? FileName { get; set; }

    [ForeignKey("Id")]
    [InverseProperty("StepContentBlob")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
