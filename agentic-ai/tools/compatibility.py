"""Deterministic red-cell compatibility rules; never delegated to an LLM."""

COMPATIBLE_DONOR_TYPES: dict[str, frozenset[str]] = {
    "OPositive": frozenset({"OPositive", "ONegative"}),
    "ONegative": frozenset({"ONegative"}),
    "APositive": frozenset({"APositive", "ANegative", "OPositive", "ONegative"}),
    "ANegative": frozenset({"ANegative", "ONegative"}),
    "BPositive": frozenset({"BPositive", "BNegative", "OPositive", "ONegative"}),
    "BNegative": frozenset({"BNegative", "ONegative"}),
    "ABPositive": frozenset(
        {
            "APositive",
            "ANegative",
            "BPositive",
            "BNegative",
            "ABPositive",
            "ABNegative",
            "OPositive",
            "ONegative",
        }
    ),
    "ABNegative": frozenset({"ANegative", "BNegative", "ABNegative", "ONegative"}),
}


def compatible_donor_types(recipient_type: str) -> frozenset[str]:
    """Return the allow-listed donor blood types for a recipient blood type."""
    return COMPATIBLE_DONOR_TYPES.get(recipient_type, frozenset())
