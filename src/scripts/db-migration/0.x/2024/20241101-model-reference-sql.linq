<Query Kind="Program">
  <NuGetReference>MiniExcel</NuGetReference>
  <Namespace>MiniExcelLibs</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

async Task Main()
{
	using HttpClient http = new();
	IEnumerable<dynamic> excelData = MiniExcel.Query(new MemoryStream(await http.GetByteArrayAsync("https://io.starworks.cc:88/cv-public/2024/ModelReference.xlsx")), useHeaderRow: true);
	ModelReference[] data = excelData.Select(x => new ModelReference()
	{
		Id = (short)x.Id,
		ProviderId = (short)x.ProviderId,
		Name = x.Name,
		MinTemperature = 0,
		MaxTemperature = (decimal)x.MaxTemperature,
		AllowSearch = x.AllowSearch == 1,
		AllowVision = x.AllowVision == 1,
		ContextWindow = (int)x.ContextWindow,
		MaxResponseTokens = (int)x.MaxResponseTokens,
		TokenizerId = x.TokenizerId is double v ? (short)v : null,
		PromptTokenPrice1M = (decimal)x.PromptTokenPrice1M,
		ResponseTokenPrice1M = (decimal)x.ResponseTokenPrice1M,
		CurrencyCode = x.Currency
	}).ToArray();
	ModelReference.ToInsertSQL(data).Dump();
}

public partial class ModelReference
{
	public short Id { get; set; }

	public short ProviderId { get; set; }

	public string Name { get; set; } = null!;

	public decimal MinTemperature { get; set; }

	public decimal MaxTemperature { get; set; }

	public bool AllowSearch { get; set; }

	public bool AllowVision { get; set; }

	public int ContextWindow { get; set; }

	public int MaxResponseTokens { get; set; }

	public short? TokenizerId { get; set; }

	public decimal PromptTokenPrice1M { get; set; }

	public decimal ResponseTokenPrice1M { get; set; }

	public string CurrencyCode { get; set; } = null!;

	public static string ToInsertSQL(ModelReference[] data)
	{
		StringBuilder sb = new();
		sb.AppendLine("""
            INSERT INTO [ModelReference] (
                Id,
                ProviderId,
                Name,
                MinTemperature,
                MaxTemperature,
                AllowSearch,
                AllowVision,
                ContextWindow,
                MaxResponseTokens,
                TokenizerId,
                PromptTokenPrice1M,
                ResponseTokenPrice1M,
                CurrencyCode
            ) VALUES
            """);
		sb.Append(string.Join(",\n", data.Select(x => x.ToInsertValues())));
		sb.Append(";");
		return sb.ToString();
	}

	private string ToInsertValues()
	{
		StringBuilder sb = new();
		sb.Append("(");
		sb.Append($"{Id}, ");
		sb.Append($"{ProviderId}, ");
		sb.Append($"'{Name.Replace("'", "''")}', ");  // Escape single quotes in strings
		sb.Append($"{MinTemperature}, ");
		sb.Append($"{MaxTemperature}, ");
		sb.Append($"{(AllowSearch ? 1 : 0)}, ");
		sb.Append($"{(AllowVision ? 1 : 0)}, ");
		sb.Append($"{ContextWindow}, ");
		sb.Append($"{MaxResponseTokens}, ");
		sb.Append($"{(TokenizerId.HasValue ? TokenizerId.ToString() : "NULL")}, ");
		sb.Append($"{PromptTokenPrice1M}, ");
		sb.Append($"{ResponseTokenPrice1M}, ");
		sb.Append($"'{CurrencyCode.Replace("'", "''")}'");  // Escape single quotes in strings
		sb.Append(")");
		return sb.ToString();
	}
}
