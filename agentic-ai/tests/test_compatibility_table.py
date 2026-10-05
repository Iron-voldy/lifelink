import pytest

from tools.compatibility import COMPATIBLE_DONOR_TYPES, compatible_donor_types

ALL_TYPES = [
    "OPositive", "ONegative", "APositive", "ANegative",
    "BPositive", "BNegative", "ABPositive", "ABNegative",
]
RH_NEGATIVE = ["ONegative", "ANegative", "BNegative", "ABNegative"]


def test_table_covers_all_eight_blood_types():
    assert set(COMPATIBLE_DONOR_TYPES) == set(ALL_TYPES)


def test_o_negative_recipient_receives_only_o_negative():
    assert compatible_donor_types("ONegative") == frozenset({"ONegative"})


def test_ab_positive_recipient_receives_all_types():
    assert compatible_donor_types("ABPositive") == frozenset(ALL_TYPES)


def test_a_positive_recipient_receives_a_and_o_types():
    assert compatible_donor_types("APositive") == frozenset(
        {"APositive", "ANegative", "OPositive", "ONegative"}
    )


@pytest.mark.parametrize("recipient", ALL_TYPES)
def test_every_recipient_can_receive_its_own_type(recipient):
    assert recipient in compatible_donor_types(recipient)


@pytest.mark.parametrize("recipient", ALL_TYPES)
def test_o_negative_is_universal_donor(recipient):
    assert "ONegative" in compatible_donor_types(recipient)


@pytest.mark.parametrize("recipient", RH_NEGATIVE)
def test_rh_negative_recipient_never_receives_rh_positive_blood(recipient):
    assert not any(donor.endswith("Positive") for donor in compatible_donor_types(recipient))


@pytest.mark.parametrize("recipient", ["unknown", "", "XPositive", "oPositive"])
def test_unknown_recipient_fails_safe_with_empty_set(recipient):
    assert compatible_donor_types(recipient) == frozenset()


def test_result_is_immutable_frozenset():
    assert isinstance(compatible_donor_types("APositive"), frozenset)