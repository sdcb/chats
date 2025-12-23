using Chats.DB;
using Chats.BE.DB.Jsons;

namespace Chats.BE.DB.Extensions;

public static class ModelExtensions
{
    extension(Model model)
    {
        public JsonPriceConfig ToPriceConfig() => new()
        {
            InputFreshTokenPrice = model.InputFreshTokenPrice1M / 100_0000,
            OutputTokenPrice = model.OutputTokenPrice1M / 100_0000,
            InputCachedTokenPrice = model.InputCachedTokenPrice1M / 100_0000,
        };
    }
}
