using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("StepContentFile")]
[Index("FileId", Name = "IX_MessageContentFile_FileId")]
public partial class StepContentFile
{
    [Key]
    public long Id { get; set; }

    public int FileId { get; set; }

    [ForeignKey("FileId")]
    [InverseProperty("StepContentFiles")]
    public virtual File File { get; set; } = null!;

    [ForeignKey("Id")]
    [InverseProperty("StepContentFile")]
    public virtual StepContent IdNavigation { get; set; } = null!;
}
