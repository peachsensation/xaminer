using AngleSharp.Dom;
using Xaminer.App.Interop.CDP;

namespace Xaminer.App.Pages
{
    public interface IContent
    {
        public Task BeforeContent(Page page);

        public Task<IDocument> GetDocument();

        public Task<IContent?> AfterNavigation(Page page);
    }

    public interface IContent<T>
    {
        public Task BeforeContent(Page page);

        public Task<IDocument> GetDocument();

        public Task<T?> AfterNavigation(Page page);
    }
}
