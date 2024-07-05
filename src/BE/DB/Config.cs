﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Keyless]
[Index("Key", Name = "Configs_key_key", IsUnique = true)]
public partial class Config
{
    [Column("key")]
    [StringLength(1000)]
    public string Key { get; set; } = null!;

    [Column("value")]
    [StringLength(1000)]
    public string Value { get; set; } = null!;

    [Column("description")]
    [StringLength(50)]
    public string? Description { get; set; }
}