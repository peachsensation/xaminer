using PhoneNumbers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using Xaminer.App.Helpers;

namespace Xaminer.App.Models
{
    public sealed record ListingsInfo(IEnumerable<Listing> Listings)
    {
        public int Version { get; } = 1;
    }

    public sealed record Listing(ListingId Id,
                                 string Name,
                                 string Place,
                                 Gender Gender,
                                 int Age,
                                 Providings Providings,
                                 Phone? Phone,
                                 Diamond Diamond,
                                 Agency? Agency,
                                 IEnumerable<Uri> Images)
    {
        [JsonIgnore]
        public string Description => $"{Id.Name} | {Gender.NameLocalized} | {Age} | {Place} | {Providings.NameLocalized}";

        public async Task<Stat> GetStat() => await UserStore.GetStat(Id);

        public bool Equals(Listing? other) =>
            Id == other?.Id &&
            string.Equals(Name, other?.Name, StringComparison.Ordinal) &&
            string.Equals(Place, other?.Place, StringComparison.Ordinal) &&
            Gender.Value == other?.Gender.Value &&
            Age == other?.Age &&
            Providings.Value == other?.Providings.Value &&
            Diamond.Value == other?.Diamond.Value;

        public override int GetHashCode() => HashCode.Combine(
            Id,
            StringComparer.Ordinal.GetHashCode(Name),
            StringComparer.Ordinal.GetHashCode(Place),
            Gender.Value,
            Age,
            Providings.Value,
            Diamond.Value);

        public override string ToString() => $"{Id} | {Age}";
    }

    public sealed partial record Diamond(DiamondEnum Value)
    {
        public string Name => Value.ToString();

        [JsonIgnore]
        public string NameLocalized => Value.GetDisplayName();

        public static readonly IDictionary<string, DiamondEnum> _names = new Dictionary<string, DiamondEnum>(StringComparer.OrdinalIgnoreCase)
        {
            {"", DiamondEnum.Unset },
            {"premium_diamond", DiamondEnum.Premium },
            {"exclusive_diamond", DiamondEnum.Exclusive }
        };

        public override string ToString() => Name;

        public static Diamond Parse(string value) => new(_names[value]);

        public static implicit operator DiamondEnum(Diamond? x) => x?.Value ?? DiamondEnum.Unset;
    }

    public sealed partial record Gender(GenderEnum Value)
    {
        public string Name => Value.ToString();

        [JsonIgnore]
        public string NameLocalized => Value.GetDisplayName();

        public static readonly IDictionary<string, GenderEnum> _names = new Dictionary<string, GenderEnum>(StringComparer.OrdinalIgnoreCase)
        {
            {"vrouw", GenderEnum.Female },
            {"woman", GenderEnum.Female },

            {"man", GenderEnum.Male },

            {"shemale", GenderEnum.Shemale },

            {"stel", GenderEnum.Couple },
            {"couple", GenderEnum.Couple }
        };

        public override string ToString() => Name;

        public static Gender Parse(string value) => _names.TryGetValue(value, out var gender)
            ? new Gender(gender)
            : new Gender(GenderEnum.Unknown);

        public static implicit operator GenderEnum(Gender? x) => x?.Value ?? GenderEnum.Unknown;
    }

    public sealed partial record Providings(ProvidingEnum Value)
    {
        public string? _name;
        public string Name
        {
            get
            {
                if (_name is not null)
                    return _name;

                var sb = new StringBuilder();
                int count = 0;

                foreach (var value in Enum.GetValues<ProvidingEnum>())
                {
                    if (Value.HasFlag(value))
                    {
                        if (count > 0)
                        {
                            sb.Append(", ");
                        }

                        sb.Append(value);
                        count++;
                    }
                }

                _name = sb.ToString();

                return _name;
            }
        }

        [JsonIgnore]
        public string NameLocalized => Value.GetDisplayName();

        public static readonly IDictionary<string, ProvidingEnum> _names = new Dictionary<string, ProvidingEnum>(StringComparer.OrdinalIgnoreCase)
        {
            {"prive ontvangst", ProvidingEnum.Home },
            {"escort inbound", ProvidingEnum.Home },

            {"escort", ProvidingEnum.Escort },

            {"Virtual Sex", ProvidingEnum.VirtualSex },

            {"erotische massage", ProvidingEnum.EroticMassage },
            {"erotic massage", ProvidingEnum.EroticMassage },

            {"BDSM", ProvidingEnum.BDSM },

            {"raamprostitutie", ProvidingEnum.RedLights },
            {"red lights", ProvidingEnum.RedLights },

            {"massagesalon", ProvidingEnum.MassageClub },
            {"massage club", ProvidingEnum.MassageClub },

            {"sexbioscoop", ProvidingEnum.Cinema },
            {"cinema", ProvidingEnum.Cinema },
        };

        public override string ToString() => Name;

        public static Providings Parse(params string[] values)
        {
            ProvidingEnum providings = 0;

            foreach (var value in values)
            {
                if (_names.TryGetValue(value, out var providing))
                {
                    providings |= providing;
                }
            }

            return new(providings);
        }

        public static implicit operator ProvidingEnum(Providings? x) => x?.Value ?? default;
    }

    public sealed partial record Agency(AgencyEnum Value)
    {
        public string Name => Value.ToString();

        [JsonIgnore]
        public string NameLocalized => Value.GetDisplayName();

        public static readonly IDictionary<string, AgencyEnum> _names = new Dictionary<string, AgencyEnum>(StringComparer.OrdinalIgnoreCase)
        {
            {"agency", AgencyEnum.Agency },
            {"virtual", AgencyEnum.Virtual }
        };

        public override string ToString() => Name;

        public static Agency? Parse(string? value) => _names.TryGetValue(value ?? "", out var agency)
            ? new Agency(agency)
            : null;

        public static implicit operator AgencyEnum?(Agency? x) => x?.Value;
    }

    public sealed partial record Phone(string Number)
    {
        private static readonly ParseOptions s_options = new ParseOptions();

        static Phone()
        {
            s_options.AllowEuropeanUnionCountries();
        }

        [return: NotNullIfNotNull(nameof(number))]
        public static Phone? Parse(string? number) => 
            number is not null && PhoneNumber.TryParse(number, s_options, out PhoneNumber? phoneNumber)
                ? new Phone(phoneNumber.ToString())
                : null;

        public NumberType Type => PhoneNumber.Parse(Number, s_options).Kind switch
        {
            PhoneNumberKind.GeographicPhoneNumber => NumberType.Fixed,
            PhoneNumberKind.MobilePhoneNumber => NumberType.Mobile,
            _ => NumberType.Unknown
        };

        public override string ToString() => Number;

        public string ToString(string format) => PhoneNumber.Parse(Number, s_options).ToString(format);
    }
}