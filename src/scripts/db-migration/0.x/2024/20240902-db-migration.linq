<Query Kind="Program">
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>Microsoft.EntityFrameworkCore.Storage</Namespace>
  <Namespace>Microsoft.Data.SqlClient</Namespace>
</Query>

void Main()
{
	ResetTablesAndIdentity();

	GuidInt64Mapping balanceLogIds = new();
	foreach (Guid id in BalanceLogs.Select(x => x.Id))
	{
		balanceLogIds.Add(id);
	}

	GuidInt32Mapping chatsIds = new();
	foreach (Guid id in Chats.Select(x => x.Id))
	{
		chatsIds.Add(id);
	}

	GuidInt64Mapping messageIds = new();
	foreach (Guid id in ChatMessages.Select(x => x.Id))
	{
		messageIds.Add(id);
	}

	Dictionary<Guid, Guid> messageBalanceMap = BalanceLogs
		.Where(x => x.MessageId != null)
		.ToDictionary(k => k.MessageId!.Value, v => v.Id);

	Dictionary<Guid, Guid> userDefaultModelId = GetUserDefaultChatModelId();

	using (var tran = Database.BeginTransaction())
	{
		using (var idInsert = new IdentityInsertScope(this, "TransactionLog"))
		{
			foreach (BalanceLogs b in BalanceLogs.AsNoTracking())
			{
				TransactionLogs.Add(new TransactionLog()
				{
					Id = balanceLogIds[b.Id],
					Amount = b.Type == (byte)DBTransactionType.Cost ? -b.Value : b.Value,
					CreatedAt = b.CreatedAt,
					CreditUserId = b.CreateUserId,
					TransactionTypeId = (byte)b.Type,
					UserId = b.UserId,
				});
			}
			SaveChanges();
		}

		List<Chats> chats = Chats.Include(x => x.ChatMessages).AsNoTracking().ToList();
		using (var idInsert = new IdentityInsertScope(this, "Conversation"))
		{
			foreach (Chats c in chats)
			{
				JsonUserModelConfig userModelConfig = JsonSerializer.Deserialize<JsonUserModelConfig>(c.UserModelConfig)!;
				Conversations.Add(new Conversation()
				{
					Id = chatsIds[c.Id],
					ChatModelId = c.ChatModelId ?? userDefaultModelId[c.UserId],
					CreatedAt = c.CreatedAt,
					Temperature = userModelConfig.Temperature,
					EnableSearch = userModelConfig.EnableSearch,
					IsDeleted = c.IsDeleted,
					IsShared = c.IsShared,
					Title = c.Title,
					UserId = c.UserId,
				});
			}
			SaveChanges();
		}

		using (var idInsert = new IdentityInsertScope(this, "Message"))
		{
			foreach (Chats c in chats)
			{
				foreach (ChatMessages msg in c.ChatMessages)
				{
					Message newMsg = new Message()
					{
						Id = messageIds[msg.Id],
						ChatRoleId = msg.Role switch
						{
							"system" => 1,
							"user" => 2,
							"assistant" => 3,
							_ => throw new NotImplementedException(),
						},
						ConversationId = chatsIds[msg.ChatId],
						CreatedAt = msg.CreatedAt,
						ParentId = msg.ParentId != null ? messageIds[msg.ParentId.Value] : null,
						UserId = msg.UserId,
						MessageResponse = msg.ChatModelId != null ? new MessageResponse()
						{
							ChatModelId = msg.ChatModelId.Value,
							DurationMs = msg.Duration,
							InputCost = msg.InputPrice,
							InputTokenCount = msg.InputTokens,
							MessageId = messageIds[msg.Id],
							OutputCost = msg.OutputPrice,
							OutputTokenCount = msg.OutputTokens,
							TransactionLogId = messageBalanceMap.TryGetValue(msg.Id, out Guid balanceId) ? balanceLogIds[balanceId] : null,
						} : null,
						//MessageContents = MakeContents(msg.Messages),
					};
					Messages.Add(newMsg);
				}
			}
			SaveChanges();
		}

		foreach (Chats c in chats)
		{
			foreach (ChatMessages msg in c.ChatMessages)
			{
				foreach (MessageContent mc in MakeContents(msg.Messages))
				{
					mc.MessageId = messageIds[msg.Id];
					MessageContents.Add(mc);
				}
			}
		}
		SaveChanges();

		tran.Commit();
	}

	List<MessageContent> MakeContents(string message)
	{
		MessageContentDto dto = JsonSerializer.Deserialize<MessageContentDto>(message)!;
		return
		[
			new MessageContent { ContentTypeId = (byte)DBMessageContentType.Text, Content = Encoding.Unicode.GetBytes(dto.Text) },
			..(dto.Image ?? []).Select(x => new MessageContent()
			{
				ContentTypeId = (byte)DBMessageContentType.ImageUrl,
				Content = Encoding.UTF8.GetBytes(x),
			})
		];
	}
}

Dictionary<Guid, Guid> GetUserDefaultChatModelId()
{
	HashSet<Guid> availableModels = ChatModels
		.OrderBy(x => x.Rank)
		.Where(x => x.Enabled)
		.Select(x => x.Id)
		.ToHashSet();

	return UserModels
		.Select(x => new { x.UserId, x.Models })
		.AsEnumerable()
		.ToDictionary(k => k.UserId, v => JsonSerializer.Deserialize<JsonTokenBalance[]>(v.Models)!
			.Select(x => x.ModelId)
			.Where(x => availableModels.Contains(x))
			.First());
}

void ResetTablesAndIdentity()
{
	using (IDbContextTransaction tran = Database.BeginTransaction())
	{
		try
		{
			// 清空表数据并重置自增 ID
			string resetTablesSql = @"
                DELETE FROM [dbo].[MessageContent]; DBCC CHECKIDENT ('[dbo].[MessageContent]', RESEED, 0);
				DELETE FROM [dbo].[MessageResponse];
                DELETE FROM [dbo].[Message]; DBCC CHECKIDENT ('[dbo].[Message]', RESEED, 0);
                DELETE FROM [dbo].[TransactionLog]; DBCC CHECKIDENT ('[dbo].[TransactionLog]', RESEED, 0);
                DELETE FROM [dbo].[Conversation]; DBCC CHECKIDENT ('[dbo].[Conversation]', RESEED, 0);
            ";

			Database.ExecuteSqlRaw(resetTablesSql);

			// 提交事务
			tran.Commit();
		}
		catch
		{
			// 回滚事务
			tran.Rollback();
			throw;
		}
	}
}

public class IdentityInsertScope : IDisposable
{
	private readonly DbContext _context;
	private readonly string _tableName;
	public const string Conversation = "Conversation";
	public const string Message = "Message";
	public const string TransactionLog = "TransactionLog";
	public const string MessageContent = "MessageContent";


	public IdentityInsertScope(DbContext context, string tableName)
	{
		_context = context;
		_tableName = tableName;
		TrySetIdentityInsert(tableName, true);
	}

	public void Dispose()
	{
		TrySetIdentityInsert(_tableName, false);
	}

	public void TrySetIdentityInsert(string table, bool enable)
	{
		try
		{
			string state = enable ? "ON" : "OFF";
			_context.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT [dbo].[{table}] {state}");
		}
		catch (SqlException ex)
		{
			// 如果我们遇到错误，可以根据需要进行处理或记录日志
			Console.WriteLine($"Error setting IDENTITY_INSERT for table '{table}': {ex.Message}");
		}
	}
}

public class GuidInt64Mapping
{
	long _nextId = 1;
	Dictionary<Guid, long> _mapping = new();

	public void Add(Guid guid)
	{
		_mapping.Add(guid, _nextId++);
	}

	public long this[Guid guid]
	{
		get
		{
			return _mapping[guid];
		}
	}
}

public class GuidInt32Mapping
{
	int _nextId = 1;
	Dictionary<Guid, int> _mapping = new();

	public void Add(Guid guid)
	{
		_mapping.Add(guid, _nextId++);
	}

	public int this[Guid guid]
	{
		get
		{
			return _mapping[guid];
		}
	}
}

public record JsonTokenBalance
{
	[JsonPropertyName("modelId")]
	public required Guid ModelId { get; init; }

	[JsonPropertyName("tokens"), JsonConverter(typeof(NumberToStringConverter))]
	public required string Tokens { get; init; }

	[JsonPropertyName("counts"), JsonConverter(typeof(NumberToStringConverter))]
	public required string Counts { get; init; }

	[JsonPropertyName("expires")]
	public required string Expires { get; init; }

	[JsonPropertyName("enabled")]
	public required bool Enabled { get; init; }
}

public class NumberToStringConverter : JsonConverter<string>
{
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number)
		{
			return reader.GetInt32().ToString();
		}
		else if (reader.TokenType == JsonTokenType.String)
		{
			return reader.GetString()!;
		}
		else
		{
			throw new JsonException($"Unsupported token type: {reader.TokenType}");
		}
	}

	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}
}

public record JsonUserModelConfig
{
	[JsonPropertyName("temperature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public float? Temperature { get; init; }

	[JsonPropertyName("enableSearch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public bool? EnableSearch { get; init; }
}

public record MessageContentDto
{
	[JsonPropertyName("text")]
	public required string Text { get; init; }

	[JsonPropertyName("image"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public List<string>? Image { get; init; }
}

public enum DBMessageContentType : byte
{
	Text = 1,
	ImageUrl = 2,
}

public enum DBTransactionType : byte
{
	Charge = 1,
	Cost = 2,
	Initial = 3,
}
