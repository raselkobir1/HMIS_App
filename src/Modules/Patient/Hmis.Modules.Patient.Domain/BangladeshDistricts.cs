namespace Hmis.Modules.Patient.Domain;

/// <summary>The 64 districts of Bangladesh (by division), used for structured addresses.</summary>
public static class BangladeshDistricts
{
    public static readonly IReadOnlyList<string> All =
    [
        // Dhaka
        "Dhaka", "Faridpur", "Gazipur", "Gopalganj", "Kishoreganj", "Madaripur", "Manikganj", "Munshiganj",
        "Narayanganj", "Narsingdi", "Rajbari", "Shariatpur", "Tangail",
        // Chattogram
        "Bandarban", "Brahmanbaria", "Chandpur", "Chattogram", "Cox's Bazar", "Cumilla", "Feni", "Khagrachhari",
        "Lakshmipur", "Noakhali", "Rangamati",
        // Rajshahi
        "Bogura", "Chapainawabganj", "Joypurhat", "Naogaon", "Natore", "Pabna", "Rajshahi", "Sirajganj",
        // Khulna
        "Bagerhat", "Chuadanga", "Jashore", "Jhenaidah", "Khulna", "Kushtia", "Magura", "Meherpur", "Narail", "Satkhira",
        // Barishal
        "Barguna", "Barishal", "Bhola", "Jhalokati", "Patuakhali", "Pirojpur",
        // Sylhet
        "Habiganj", "Moulvibazar", "Sunamganj", "Sylhet",
        // Rangpur
        "Dinajpur", "Gaibandha", "Kurigram", "Lalmonirhat", "Nilphamari", "Panchagarh", "Rangpur", "Thakurgaon",
        // Mymensingh
        "Jamalpur", "Mymensingh", "Netrokona", "Sherpur"
    ];

    private static readonly HashSet<string> Lookup = new(All, StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? district) => district is null || Lookup.Contains(district);
}
