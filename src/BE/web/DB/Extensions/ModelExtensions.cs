using Chats.DB;
using Chats.BE.DB.Jsons;

namespace Chats.BE.DB.Extensions;

public static class ModelExtensions
{
    extension(Model model)
    {
        public JsonPriceConfig ToPriceConfig() => new()
        {
            InputFreshTokenPrice = model.CurrentSnapshot.InputFreshTokenPrice1M / 100_0000,
            OutputTokenPrice = model.CurrentSnapshot.OutputTokenPrice1M / 100_0000,
            InputCachedTokenPrice = model.CurrentSnapshot.InputCachedTokenPrice1M / 100_0000,
        };
    }
}
