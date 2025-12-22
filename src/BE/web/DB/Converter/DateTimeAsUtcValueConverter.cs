using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chats.Web.DB.Converter;

public class DateTimeAsUtcValueConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
