using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatRunEvent")]
[Index("RunId", "FirstSeq", Name = "UX_ChatRunEvent_Run_FirstSeq", IsUnique = true)]
public partial class ChatRunEvent
{
    [Key]
    public long Id { get; set; }

    public long RunId { get; set; }

    public int FirstSeq { get; set; }

    public int LastSeq { get; set; }

    public string Payload { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    [ForeignKey("RunId")]
    [InverseProperty("ChatRunEvents")]
    public virtual ChatRun Run { get; set; } = null!;
}
