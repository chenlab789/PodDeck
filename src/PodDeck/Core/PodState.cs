namespace PodDeck.Core;

/// <summary>Snapshot decoded from one Apple "proximity pairing" BLE advertisement (type 0x07).</summary>
public sealed record PodState
{
    public ushort Model { get; init; }
    public string ModelName { get; init; } = "AirPods";

    /// <summary>Battery 0-100, or null when unknown (nibble 0xF).</summary>
    public int? Left { get; init; }
    public int? Right { get; init; }
    public int? Case { get; init; }

    public bool LeftCharging { get; init; }
    public bool RightCharging { get; init; }
    public bool CaseCharging { get; init; }

    public bool LeftInEar { get; init; }
    public bool RightInEar { get; init; }

    public byte Status { get; init; }
    public byte LidByte { get; init; }
    public short Rssi { get; init; }
    public DateTime SeenAt { get; init; }

    /// <summary>Number of pods currently in an ear (0-2). Side is not reliable, the count is.</summary>
    public int InEarCount { get; init; }
    /// <summary>Broadcasting pod is docked in the case (status bit 0x04).</summary>
    public bool InCase { get; init; }
    /// <summary>Case lid open: pods docked and lid byte bit 0x08 clear (calibrated on AirPods 4 ANC).</summary>
    public bool LidOpen => InCase && (LidByte & 0x08) == 0;

    public bool AnyInEar => InEarCount > 0;

    static readonly Dictionary<ushort, string> Models = new()
    {
        [0x2002] = "AirPods",
        [0x200F] = "AirPods (2nd gen)",
        [0x2013] = "AirPods (3rd gen)",
        [0x2019] = "AirPods 4",
        [0x201B] = "AirPods 4",
        [0x200E] = "AirPods Pro",
        [0x2014] = "AirPods Pro 2",
        [0x2024] = "AirPods Pro 2",
        [0x2027] = "AirPods Pro 3",
        [0x200A] = "AirPods Max",
        [0x201F] = "AirPods Max",
    };

    public static bool IsKnownModel(ushort model) => Models.ContainsKey(model);

    /// <summary>
    /// Parse manufacturer data for company 0x004C (bytes after the company id).
    /// Layout: [0]=0x07 [1]=len [2]=prefix [3..4]=model LE [5]=status [6]=pod battery nibbles
    /// [7]=charge flags (hi) + case battery (lo) [8]=lid/open counter [9]=color ...
    /// </summary>
    public static PodState? TryParse(ReadOnlySpan<byte> d, short rssi)
    {
        if (d.Length < 10 || d[0] != 0x07) return null;
        ushort model = (ushort)(d[3] | (d[4] << 8));
        if (!IsKnownModel(model)) return null;

        byte status = d[5];
        // When bit 0x20 is set, the broadcasting pod is the left one and nibbles are swapped.
        bool flip = (status & 0x20) != 0;
        int hi = d[6] >> 4, lo = d[6] & 0x0F;
        int leftN = flip ? lo : hi;
        int rightN = flip ? hi : lo;

        int chg = d[7] >> 4;
        int caseN = d[7] & 0x0F;

        // In-ear bits (calibrated on AirPods 4 ANC): 0x02 = broadcasting pod, 0x08 = other pod.
        bool thisInEar = (status & 0x02) != 0;
        bool otherInEar = (status & 0x08) != 0;

        return new PodState
        {
            Model = model,
            ModelName = Models[model],
            Left = Level(leftN),
            Right = Level(rightN),
            Case = Level(caseN),
            LeftCharging = (chg & (flip ? 0b0010 : 0b0001)) != 0,
            RightCharging = (chg & (flip ? 0b0001 : 0b0010)) != 0,
            CaseCharging = (chg & 0b0100) != 0,
            LeftInEar = flip ? thisInEar : otherInEar,
            RightInEar = flip ? otherInEar : thisInEar,
            InEarCount = (thisInEar ? 1 : 0) + (otherInEar ? 1 : 0),
            InCase = (status & 0x04) != 0,
            Status = status,
            LidByte = d[8],
            Rssi = rssi,
            SeenAt = DateTime.Now,
        };
    }

    static int? Level(int nibble) => nibble switch
    {
        0x0F => null,
        > 10 => 100,
        _ => nibble * 10,
    };
}
