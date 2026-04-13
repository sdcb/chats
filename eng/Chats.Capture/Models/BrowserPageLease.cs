using Microsoft.Playwright;

namespace Chats.Capture.Models;

public sealed class BrowserPageLease : IAsyncDisposable
{
  public BrowserPageLease(IBrowserContext context, IPage page)
  {
    Context = context;
    Page = page;
  }

  public IBrowserContext Context { get; }

  public IPage Page { get; }

  public async ValueTask DisposeAsync()
  {
    await Context.CloseAsync();
  }
}