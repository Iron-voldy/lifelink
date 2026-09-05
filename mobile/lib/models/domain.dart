class DonorProfile {
  const DonorProfile(
      {required this.id,
      required this.bloodType,
      required this.dateOfBirth,
      required this.address,
      required this.eligibilityStatus,
      required this.medicalFlags,
      this.latitude,
      this.longitude});
  final String id, bloodType, dateOfBirth, address, eligibilityStatus;
  final List<String> medicalFlags;
  final double? latitude, longitude;
  factory DonorProfile.fromJson(Map<String, dynamic> j) => DonorProfile(
      id: j['id'],
      bloodType: j['bloodType'],
      dateOfBirth: j['dateOfBirth'],
      address: j['address'],
      eligibilityStatus: j['eligibilityStatus'],
      medicalFlags: List<String>.from(j['medicalFlags'] ?? []),
      latitude: (j['latitude'] as num?)?.toDouble(),
      longitude: (j['longitude'] as num?)?.toDouble());
}

class BloodRequest {
  const BloodRequest(
      {required this.id,
      required this.bloodType,
      required this.quantityUnits,
      required this.urgency,
      required this.status,
      required this.requiredByUtc,
      this.notes});
  final String id, bloodType, urgency, status, requiredByUtc;
  final int quantityUnits;
  final String? notes;
  factory BloodRequest.fromJson(Map<String, dynamic> j) => BloodRequest(
      id: j['id'],
      bloodType: j['bloodType'],
      quantityUnits: j['quantityUnits'],
      urgency: j['urgency'],
      status: j['status'],
      requiredByUtc: j['requiredByUtc'],
      notes: j['notes']);
}

class Camp {
  const Camp(
      {required this.id,
      required this.name,
      required this.location,
      required this.startsAtUtc,
      required this.availableSlots,
      this.latitude,
      this.longitude});
  final String id, name, location, startsAtUtc;
  final int availableSlots;
  final double? latitude, longitude;
  factory Camp.fromJson(Map<String, dynamic> j) => Camp(
      id: j['id'],
      name: j['name'],
      location: j['location'],
      startsAtUtc: j['startsAtUtc'],
      availableSlots: j['availableSlots'],
      latitude: (j['latitude'] as num?)?.toDouble(),
      longitude: (j['longitude'] as num?)?.toDouble());
}

class EligibilityEvaluation {
  const EligibilityEvaluation(
      {required this.status, required this.reasons, required this.previous});
  final String status, previous;
  final List<String> reasons;
  factory EligibilityEvaluation.fromJson(Map<String, dynamic> j) =>
      EligibilityEvaluation(
          status: j['status'],
          previous: j['previousStatus'],
          reasons: List<String>.from(j['reasons'] ?? []));
}

class EligibilityHistoryEntry {
  const EligibilityHistoryEntry(
      {required this.previous,
      required this.next,
      required this.reason,
      required this.changedAtUtc});
  final String previous, next, reason, changedAtUtc;
  factory EligibilityHistoryEntry.fromJson(Map<String, dynamic> j) =>
      EligibilityHistoryEntry(
          previous: j['previousStatus'],
          next: j['newStatus'],
          reason: j['reason'] ?? '',
          changedAtUtc: j['changedAtUtc']);
}

class DonationRecord {
  const DonationRecord(
      {required this.date,
      required this.units,
      required this.location,
      this.notes});
  final String date, location;
  final int units;
  final String? notes;
  factory DonationRecord.fromJson(Map<String, dynamic> j) => DonationRecord(
      date: j['donationDate'],
      units: j['units'],
      location: j['location'],
      notes: j['notes']);
}

class CampSlot {
  const CampSlot(
      {required this.id,
      required this.slotTimeUtc,
      required this.status,
      this.donorId});
  final String id, slotTimeUtc, status;
  final String? donorId;
  bool get isOpen => status == 'Available';
  factory CampSlot.fromJson(Map<String, dynamic> j) => CampSlot(
      id: j['id'],
      slotTimeUtc: j['slotTimeUtc'],
      status: j['status'],
      donorId: j['donorId']);
}

class RequestHistoryEntry {
  const RequestHistoryEntry(
      {required this.previous,
      required this.next,
      required this.changedAtUtc,
      this.reason});
  final String previous, next, changedAtUtc;
  final String? reason;
  factory RequestHistoryEntry.fromJson(Map<String, dynamic> j) =>
      RequestHistoryEntry(
          previous: j['previousStatus'],
          next: j['newStatus'],
          changedAtUtc: j['changedAtUtc'],
          reason: j['reason']);
}

class AppNotification {
  const AppNotification(
      {required this.type,
      required this.status,
      required this.createdAtUtc,
      this.sentAtUtc});
  final String type, status, createdAtUtc;
  final String? sentAtUtc;
  factory AppNotification.fromJson(Map<String, dynamic> j) => AppNotification(
      type: j['type'],
      status: j['status'],
      createdAtUtc: j['createdAtUtc'],
      sentAtUtc: j['sentAtUtc']);
}
