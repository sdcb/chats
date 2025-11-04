<Query Kind="Program">
  <NuGetReference>Microsoft.CodeAnalysis.Analyzers</NuGetReference>
  <NuGetReference>Microsoft.CodeAnalysis.CSharp</NuGetReference>
  <Namespace>Microsoft.CodeAnalysis</Namespace>
  <Namespace>Microsoft.CodeAnalysis.CSharp</Namespace>
  <Namespace>Microsoft.CodeAnalysis.CSharp.Syntax</Namespace>
  <Namespace>static Microsoft.CodeAnalysis.CSharp.SyntaxFactory</Namespace>
  <Namespace>System.Data.Linq.Mapping</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

void Main()
{
	Environment.CurrentDirectory = Path.GetDirectoryName(Util.CurrentQueryPath)!;
	TableDef[] tableDefs = new string[]
	{
		"ChatRole",
		"FinishReason",
		"StepContentType",
		"TransactionType",
	}
	.Select(tableName => TableDef.FromContextTableDef(this, tableName))
	.ToArray();
	SyntaxNode cu = GenerateCompilationUnit(tableDefs);
	Util.FixedFont(cu.ToFullString()).Dump();
	System.IO.File.WriteAllText("BasicData.cs", cu.ToFullString());
}

static CompilationUnitSyntax GenerateCompilationUnit(TableDef[] tableDefs)
{
	var usingDef = static (string ns) => UsingDirective(
		Token(SyntaxKind.UsingKeyword).WS(), default, default, ParseName(ns), Token(SyntaxKind.SemicolonToken).WS("\n"));
	var fileScopedNamespaceDef = static (string ns) => FileScopedNamespaceDeclaration(
		default, default, Token(SyntaxKind.NamespaceKeyword).WS(), ParseName(ns), Token(SyntaxKind.SemicolonToken).WS("\n\n"), default, default, default);
	var classDef = static (string name) => ClassDeclaration(
		default, [Token(SyntaxKind.InternalKeyword).WS(), Token(SyntaxKind.StaticKeyword).WS()], Token(SyntaxKind.ClassKeyword).WS(), Identifier(name).WS("\n"),
		default, default, default, Token(SyntaxKind.OpenBraceToken).WS("\n"), default, Token(SyntaxKind.CloseBraceToken), Token(SyntaxKind.SemicolonToken));
	CompilationUnitSyntax cu = CompilationUnit()
		.AddUsings(
			usingDef("Chats.BE.DB").WS("\n\n")
		)
		.AddMembers(
			fileScopedNamespaceDef("Chats.BE.DB.Init").AddMembers(
				classDef("BasicData")
					.AddMembers(
					[
						InsertAllMethod(tableDefs),
						..tableDefs.Select((x, i) => InsertSimpleDataMethod(x, needEmptyLine: i != tableDefs.Length - 1)).ToArray()
					]))
		);
	return cu;
}

static MethodDeclarationSyntax InsertAllMethod(TableDef[] defs)
{
	var im = new IdentManager(4);
	return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)).WS(), $"InsertAll")
		.AddModifiers(Token(SyntaxKind.PublicKeyword).WS(), Token(SyntaxKind.StaticKeyword).WS())
		.AddParameterListParameters(Parameter(Identifier("db")).WithType(IdentifierName("ChatsDB").WS()))
		.WithLeadingTrivia(im.WS())
		.WS("\n")
		.WithBody(Block(
			im.OpenBrace(),
			[..defs.Select(def => ExpressionStatement(
				InvocationExpression(ParseName(def.InsertMethodName), ArgumentList([Argument(ParseName("db"))])))
				.WithLeadingTrivia(im.WS())
				.WS("\n")
				)],
			im.CloseBrace())
		).WS("\n\n");
}

static MethodDeclarationSyntax InsertSimpleDataMethod(TableDef def, bool needEmptyLine)
{
	var im = new IdentManager(4);
	return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)).WS(), def.InsertMethodName)
		.AddModifiers(Token(SyntaxKind.PrivateKeyword).WS(), Token(SyntaxKind.StaticKeyword).WS())
		.AddParameterListParameters(Parameter(Identifier("db")).WithType(IdentifierName("ChatsDB").WS()))
		.WithLeadingTrivia(im.WS())
		.WS("\n")
		.WithBody(Block(
			im.OpenBrace(),
			[ExpressionStatement(
				InvocationExpression(ParseName($"db.{def.PropertyName}.AddRange"),
				ArgumentList(
					im.OpenParen(),
					[Argument(CollectionExpression(
						im.OpenBracket(),
						SeparatedList<CollectionElementSyntax>(def.TableData.Value.Select(x => def.ToExpression(x).WithLeadingTrivia(im.WS())),
							Enumerable.Repeat(Token(SyntaxKind.CommaToken).WS("\n"), def.TableData.Value.Length - 1)),
						im.CloseBracket()
						))],
					im.CloseParen()
				)))
				.WithLeadingTrivia(im.WS(), Comment($"// Generated from data, hash: {def.GenerateDataHash()}\n"), im.WS())
				.WS("\n")
			],
			im.CloseBrace()).WS("\n" + (needEmptyLine ? "\n" : ""))
		);
}

static int GetValueDisplayLength(object? value)
{
	return value switch
	{
		null => 4,
		string str => GetStringDisplayLength(str),
		decimal d => d.ToString().Length + 1,
		DateOnly d => 26,
		not null => value.ToString()!.Length,
	};

	static int GetStringDisplayLength(string str)
	{
		if (str.Contains('"'))
		{
			// 使用原始字符串字面量
			int quoteCount = GetRequiredQuoteCount(str);
			int quoteStringLength = quoteCount * 2; // 起始和结束的引号
			return str.Length + quoteStringLength;
		}
		else
		{
			// 常规字符串字面量，需要考虑转义的引号
			int escapedLength = str.Length + str.Count(ch => ch == '"');
			return escapedLength + 2; // 包含开头和结尾的引号
		}
	}
}

static int GetRequiredQuoteCount(string value)
{
	int maxQuoteCount = MaxConsecutiveChar(value, '"');
	return Math.Max(3, maxQuoteCount + 1);
}

// 辅助方法：计算字符串中连续引号的最大数量
static int MaxConsecutiveChar(string s, char c)
{
	int maxCount = 0;
	int currentCount = 0;
	foreach (char ch in s)
	{
		if (ch == c)
		{
			currentCount++;
			if (currentCount > maxCount)
				maxCount = currentCount;
		}
		else
		{
			currentCount = 0;
		}
	}
	return maxCount;
}

static ExpressionSyntax ValueToLiteral(object? value)
{
	return value switch
	{
		null => SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression),

		byte byteValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(byteValue)),

		int intValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(intValue)),

		short shortValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(shortValue)),

		decimal decimalValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(decimalValue)),

		double doubleValue => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(doubleValue)),

		bool boolValue => boolValue
			? SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression)
			: SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression),

		DateOnly date => ObjectCreationExpression(
			Token(SyntaxKind.NewKeyword).WithTrailingTrivia(Whitespace(" ")),
			IdentifierName("DateOnly"),
			ArgumentList(
				Token(SyntaxKind.OpenParenToken),
				SeparatedList<ArgumentSyntax>(
				[
					Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(date.Year))),
					Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(date.Month).WithLeadingTrivia(Whitespace(date.Month < 10 ? " " : "")))),
					Argument(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(date.Day).WithLeadingTrivia(Whitespace(date.Day < 10 ? " " : "")))),
				],
				[
					Token(SyntaxKind.CommaToken).WithTrailingTrivia(Whitespace(" ")),
					Token(SyntaxKind.CommaToken).WithTrailingTrivia(Whitespace(" ")),
				]),
				Token(SyntaxKind.CloseParenToken)),
			null),

		string stringValue => stringValue switch
		{
			var x when x.Contains('"') => CreateRawStringLiteralExpression(stringValue),
			_ => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(stringValue)),
		},

		_ => throw new ArgumentException("Unsupported literal type", value.GetType().FullName)
	};

	static LiteralExpressionSyntax CreateRawStringLiteralExpression(string value)
	{
		int quoteCount = GetRequiredQuoteCount(value);
		string quoteString = new string('"', quoteCount);
		string rawStringLiteralText = quoteString + value + quoteString;

		var literalToken = SyntaxFactory.Token(
			SyntaxTriviaList.Empty,
			SyntaxKind.SingleLineRawStringLiteralToken,
			rawStringLiteralText,
			value,
			SyntaxTriviaList.Empty
		);

		return LiteralExpression(SyntaxKind.StringLiteralExpression, literalToken);
	}
}

record TableDef
{
	public Type Type { get; }

	public Lazy<object[]> TableData { get; }
	
	public string PropertyName => Type.Name + "s";
	
	public string InsertMethodName => $"Insert{Type.Name}s";

	public FieldInfo[] Fields { get; }

	public Lazy<Dictionary<string, int>> FieldsMaxLength { get; }

	public static TableDef FromContextTableDef(object me, string tableName)
	{
		PropertyInfo prop = me.GetType().GetProperty($"{tableName}s") ?? throw new InvalidOperationException($"Property {tableName}s not found.");
		Type propType = prop.PropertyType.GenericTypeArguments[0];
		IEnumerable dataTable = (IEnumerable)prop.GetValue(me)!;
		return new TableDef(propType, new Lazy<object[]>(() => dataTable.OfType<object>().ToArray()));
	}

	public TableDef(Type type, Lazy<object[]> tableData)
	{
		Type = type;
		TableData = tableData;
		Fields = Type
			.GetFields()
			.Where(x => x.GetCustomAttribute<ColumnAttribute>() != null)
			.ToArray();
		FieldsMaxLength = new Lazy<Dictionary<string, int>>(() => Fields
			.ToDictionary(k => k.Name, v => TableData.Value.Max(data => GetValueDisplayLength(v.GetValue(data)))));
	}

	public string GenerateDataHash()
	{
		using (SHA256 sha256 = SHA256.Create())
		{
			StringBuilder builder = new StringBuilder();

			foreach (object item in TableData.Value)
			{
				if (item != null)
				{
					foreach (FieldInfo field in Fields)
					{
						object? value = field.GetValue(item);
						builder.Append(value?.ToString() ?? string.Empty);
					}
				}
			}

			byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));

			StringBuilder hashString = new StringBuilder();
			foreach (byte b in hashBytes)
			{
				hashString.Append(b.ToString("x2"));
			}

			return hashString.ToString();
		}
	}

	public ExpressionElementSyntax ToExpression(object data)
	{
		return ExpressionElement(
			ImplicitObjectCreationExpression(
				ArgumentList(),
				InitializerExpression(
					SyntaxKind.ObjectInitializerExpression,
					Token(SyntaxKind.OpenBraceToken).WS(),
					SeparatedList<ExpressionSyntax>(Fields.Select(x => AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
						IdentifierName(x.Name),
						ValueToLiteral(x.GetValue(data)))),
						Fields.Select(x => Token(SyntaxKind.CommaToken)
							.WS(new string(' ', FieldsMaxLength.Value[x.Name] - GetValueDisplayLength(x.GetValue(data)) + 1)))
					),
					Token(SyntaxKind.CloseBraceToken)
				)
			)
		);
	}
}

public static class SyntaxExtensions
{
	public static TSyntax WS<TSyntax>(this TSyntax node, string text = " ") where TSyntax : SyntaxNode
	{
		return node.WithTrailingTrivia(Whitespace(text));
	}

	public static SyntaxToken WS(this SyntaxToken token, string text = " ")
	{
		return token.WithTrailingTrivia(Whitespace(text));
	}
}

class IdentManager
{
	private int ident;
	private readonly int step;
	private readonly char identChar;

	public IdentManager(int ident, int step = 4, char identChar = ' ')
	{
		this.ident = ident;
		this.step = step;
		this.identChar = identChar;
	}

	// {}
	public SyntaxToken OpenBrace() => Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(BeginWS()).WS("\n");
	public SyntaxToken CloseBrace()
	{
		ident -= step;
		return Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(WS()).WS("\n");
	}

	// []
	public SyntaxToken OpenBracket() => Token(SyntaxKind.OpenBracketToken).WithLeadingTrivia(BeginWS()).WS("\n");
	public SyntaxToken CloseBracket()
	{
		ident -= step;
		return Token(SyntaxKind.CloseBracketToken).WithLeadingTrivia(Whitespace("\n"), WS());
	}

	// ()
	public SyntaxToken OpenParen() => Token(SyntaxKind.OpenParenToken).WS("\n");
	public SyntaxToken CloseParen() => Token(SyntaxKind.CloseParenToken);

	public SyntaxTrivia BeginWS()
	{
		SyntaxTrivia ws = WS();
		ident += step;
		return ws;
	}

	public SyntaxTrivia WS(int offset = 0) => Whitespace(new string(identChar, ident + offset));
}