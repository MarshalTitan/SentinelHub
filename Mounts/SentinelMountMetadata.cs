namespace SentinelHub.Mounts;

// Sentinel-authored factual collection definitions, independently assembled from official patch notes.
// These are exact English game-data lookup keys, not UI translations or a redistributed mount dataset.
// Expansion assignment is limited to these verified members/rewards; no row-ID or item-level heuristics.
public static class SentinelMountMetadata
{
    public const string Revision = "2026-10-17";
    public static readonly MountTrackSeed[] Tracks =
    [
        new("arr-extreme", 0, "Kirin", "A Legend for a Legend",
            "https://eu.finalfantasyxiv.com/lodestone/topics/detail/5d677f92bbe71478ffaccd9972735b3d4280abe6",
            ["Aithon", "Xanthos", "Gullfaxi", "Enbarr", "Markab", "Boreas"]),
        new("hw-extreme", 1, "Firebird", "Fiery Wings, Fiery Hearts",
            "https://jp.finalfantasyxiv.com/lodestone/topics/detail/17a38621ab0637f4ec5fbb329a8fb18b38ee09c3",
            ["White Lanner", "Rose Lanner", "Round Lanner", "Warring Lanner", "Dark Lanner", "Sophic Lanner", "Demonic Lanner"]),
        new("sb-extreme", 2, "Kamuy of the Nine Tails", "A Lone Wolf No More",
            "https://eu.finalfantasyxiv.com/lodestone/topics/detail/97afed7c0efbbe8260755f948d0cf2b912d71121",
            ["Blissful Kamuy", "Reveling Kamuy", "Legendary Kamuy", "Auspicious Kamuy", "Lunar Kamuy", "Euphonious Kamuy", "Hallowed Kamuy"]),
        new("shb-extreme", 3, "Landerwaffe", "The Dragon Made",
            "https://eu.finalfantasyxiv.com/lodestone/topics/detail/1cc2ab61c569bf2d07deeb95d7fb407013d3d9df/",
            ["Fae Gwiber", "Innocent Gwiber", "Shadow Gwiber", "Ruby Gwiber", "Gwiber of Light", "Emerald Gwiber", "Diamond Gwiber"]),
        new("ew-extreme", 4, "Apocryphal Bahamut", "Wings of Hope",
            "https://jp.finalfantasyxiv.com/lodestone/topics/detail/e17ce5b98068f6972379cef5adbc6c4b664f9780",
            ["Lynx of Eternal Darkness", "Lynx of Divine Light", "Bluefeather Lynx", "Lynx of Imperious Wind", "Lynx of Righteous Fire", "Lynx of Fallen Shadow", "Lynx of Abyssal Grief"]),
        new("dt-extreme", 5, "Wings of Legacy", "The Wing Spirit Cometh",
            "https://fr.finalfantasyxiv.com/lodestone/topics/detail/9beb8a7b5c46944cd80ed92b2b8e972395fbea80",
            ["Wings of Ruin", "Wings of Resolve", "Wings of Eternity", "Wings of the Knighthood", "Wings of Death", "Wings of Mist", "Wings of Nihility"]),
    ];
    public static MountTrackSeed? Find(string englishName) => Tracks.FirstOrDefault(t =>
        t.Reward.Equals(englishName, StringComparison.OrdinalIgnoreCase) ||
        t.Members.Contains(englishName, StringComparer.OrdinalIgnoreCase));
}
