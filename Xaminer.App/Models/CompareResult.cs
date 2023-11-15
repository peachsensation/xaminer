namespace Xaminer.App.Models
{
    public sealed record CompareResult(
        IEnumerable<ChangeListing> All,
        IEnumerable<ChangeListing> Females,
        IEnumerable<ChangeListing> Males,
        IEnumerable<ChangeListing> Shemales,
        IEnumerable<ChangeListing> Couples)
    {
        public static CompareResult FromAll(IEnumerable<ChangeListing> all) => 
            new
            (
                All: all,
                Females: all.Where(x => x.Listing.Gender == GenderEnum.Female),
                Males: all.Where(x => x.Listing.Gender == GenderEnum.Male),
                Shemales: all.Where(x => x.Listing.Gender == GenderEnum.Shemale),
                Couples: all.Where(x => x.Listing.Gender == GenderEnum.Couple)
            );
    }
}
