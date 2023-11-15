using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

namespace Xaminer.App.Helpers
{
    internal static class EnumHelper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicFields)] TEnum> where TEnum : struct, Enum
    {
        static EnumHelper()
        {
            var values = Enum.GetValues<TEnum>();

            foreach (var value in values)
            {
                s_metadataCache.Add(value, GetEnumMetadata(value));
            }
        }

        private static readonly IDictionary<TEnum, EnumMetadata> s_metadataCache = new Dictionary<TEnum, EnumMetadata>();

        public static string GetDisplayName(TEnum value)
        {
            if (typeof(TEnum).IsDefined(typeof(FlagsAttribute), inherit: false))
            {
                var number = Convert.ToInt32(value);

                var enumValues = Enum.GetValues<TEnum>()
                    .Cast<int>()
                    .Where(x => x != 0 && (x & number) == x)
                    .Cast<TEnum>()
                    .Select(x => x);

                var sb = new StringBuilder();

                for (int i = 0; i < enumValues.Count(); i++)
                {
                    var enumValue = enumValues.ElementAt(i);

                    sb.Append(s_metadataCache[enumValue].DisplayName ?? enumValue.ToString());

                    if (i < (enumValues.Count() -1))
                    {
                        sb.Append(", ");
                    }
                }

                return sb.ToString();
            }
            else
            {
                return s_metadataCache[value].DisplayName ?? value.ToString();
            }
        }

        public static IEnumerable<TEnum> GetValues() =>
            s_metadataCache
            .OrderBy(x => x.Value.Order)
            .Select(x => x.Key)
            .ToArray();

        private static EnumMetadata GetEnumMetadata(TEnum value)
        {
            var displayAttribute = typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<DisplayAttribute>();

            return new EnumMetadata
            (
                DisplayName: displayAttribute?.GetName(),
                Order: displayAttribute?.GetOrder()
            );
        }

        private sealed record EnumMetadata(string? DisplayName, int? Order);
    }

    internal static class EnumExtensions
    {
        public static string GetDisplayName<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(
            this TEnum value) where TEnum : struct, Enum => EnumHelper<TEnum>.GetDisplayName(value);
    }
}
