using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("SmsType")]
public partial class SmsType
{
    [Key]
    public byte Id { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string Name { get; set; } = null!;

    [InverseProperty("Type")]
    public virtual ICollection<SmsRecord> SmsRecords { get; set; } = new List<SmsRecord>();
}
