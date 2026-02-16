using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("RequestTracePayload")]
public partial class RequestTracePayload
{
    [Key]
    public long LogId { get; set; }

    public string RequestHeaders { get; set; } = null!;

    public string? ResponseHeaders { get; set; }

    public string? RequestBody { get; set; }

    public string? ResponseBody { get; set; }

    public byte[]? RequestBodyRaw { get; set; }

    public byte[]? ResponseBodyRaw { get; set; }

    [ForeignKey("LogId")]
    [InverseProperty("RequestTracePayload")]
    public virtual RequestTrace Log { get; set; } = null!;
}
