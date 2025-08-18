using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentType")]
public partial class StepContentType
{
    [Key]
    public byte Id { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string ContentType { get; set; } = null!;

    [InverseProperty("ContentType")]
    public virtual ICollection<StepContent> StepContents { get; set; } = new List<StepContent>();
}
