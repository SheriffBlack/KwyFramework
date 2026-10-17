namespace KwyPecvd.Device.RfMatcher.Mks;

public readonly record struct MksRfMatcherStatus(
    RfMatcherMode Mode,
    double TunePosition,
    double LoadPosition,
    bool IsOutputOn,
    bool IsMatched,
    bool IsFaulted);
