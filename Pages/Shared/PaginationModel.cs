namespace Abs.FixedAssets.Pages.Shared
{
    public class PaginationModel
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }
        public string BaseUrl { get; set; } = "";
        public string ExtraQueryParams { get; set; } = "";
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 1;
    }
}
