using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("SmsRecord")]
[Index("PhoneNumber", Name = "IX_SmsHistory_PhoneNumber")]
[Index("UserId", Name = "IX_SmsRecord_UserId")]
public partial class SmsRecord
{
    [Key]
    public int Id { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string PhoneNumber { get; set; } = null!;

    public byte TypeId { get; set; }

    public byte StatusId { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ExpectedCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public int? UserId { get; set; }

    [InverseProperty("SmsRecord")]
    public virtual ICollection<SmsAttempt> SmsAttempts { get; set; } = new List<SmsAttempt>();

    [ForeignKey("StatusId")]
    [InverseProperty("SmsRecords")]
    public virtual SmsStatus Status { get; set; } = null!;

    [ForeignKey("TypeId")]
    [InverseProperty("SmsRecords")]
    public virtual SmsType Type { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("SmsRecords")]
    public virtual User? User { get; set; }
}
