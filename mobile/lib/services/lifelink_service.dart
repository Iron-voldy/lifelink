import '../models/domain.dart';
import 'api_client.dart';

class LifeLinkService {
  LifeLinkService(this.api);
  final ApiClient api;
  Future<DonorProfile?> myDonorProfile() async {
    try {
      return DonorProfile.fromJson(await api.get('/api/donors/me'));
    } on ApiException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<DonorProfile> saveDonorProfile(
      {String? id,
      required String bloodType,
      required String dateOfBirth,
      required String address,
      double? latitude,
      double? longitude,
      List<String> flags = const []}) async {
    final body = {
      'bloodType': bloodType,
      'dateOfBirth': dateOfBirth,
      'address': address,
      'latitude': latitude,
      'longitude': longitude,
      'medicalFlags': flags
    };
    return DonorProfile.fromJson(id == null
        ? await api.post('/api/donors', body)
        : await api.put('/api/donors/$id', body));
  }

  Future<List<BloodRequest>> requests() async {
    final j = await api.get('/api/requests') as Map<String, dynamic>;
    return (j['items'] as List).map((x) => BloodRequest.fromJson(x)).toList();
  }

  Future<BloodRequest> createRequest(Map<String, dynamic> input) async =>
      BloodRequest.fromJson(await api.post('/api/requests', input));
  Future<BloodRequest> updateRequest(
          String id, Map<String, dynamic> input) async =>
      BloodRequest.fromJson(await api.put('/api/requests/$id', input));
  Future<void> registerHospital(Map<String, dynamic> input) async =>
      api.post('/api/hospitals/register', input);

  /// The signed-in staff member's hospital (with its verification status), or null before registration.
  Future<Map<String, dynamic>?> myHospital() async {
    try {
      return await api.get('/api/hospitals/me') as Map<String, dynamic>;
    } on ApiException catch (e) {
      if (e.statusCode == 404) return null;
      rethrow;
    }
  }

  Future<BloodRequest> escalate(String id, String reason) async =>
      BloodRequest.fromJson(
          await api.post('/api/requests/$id/escalate', {'reason': reason}));
  Future<List<Camp>> camps() async {
    // Only camps that have not started yet can be booked, so hide the rest.
    final from =
        Uri.encodeQueryComponent(DateTime.now().toUtc().toIso8601String());
    final j =
        await api.get('/api/camps?status=Scheduled&fromUtc=$from&pageSize=100')
            as Map<String, dynamic>;
    return (j['items'] as List).map((x) => Camp.fromJson(x)).toList();
  }

  Future<void> bookCamp(String id, {String? slotId}) async =>
      api.post('/api/camps/$id/slots/book', {'preferredSlotId': slotId});
  Future<List<CampSlot>> campSlots(String id) async =>
      ((await api.get('/api/camps/$id/slots')) as List)
          .map((x) => CampSlot.fromJson(x))
          .toList();
  Future<void> cancelBooking(String campId, String slotId) =>
      api.delete('/api/camps/$campId/slots/$slotId/booking');

  Future<EligibilityEvaluation> checkEligibility(String donorId) async =>
      EligibilityEvaluation.fromJson(
          await api.post('/api/donors/$donorId/check-eligibility', {}));
  Future<List<EligibilityHistoryEntry>> eligibilityHistory(
          String donorId) async =>
      ((await api.get('/api/donors/$donorId/eligibility-history')) as List)
          .map((x) => EligibilityHistoryEntry.fromJson(x))
          .toList();
  Future<List<DonationRecord>> donationHistory(String donorId) async =>
      ((await api.get('/api/donors/$donorId/donation-history')) as List)
          .map((x) => DonationRecord.fromJson(x))
          .toList();

  Future<List<RequestHistoryEntry>> requestHistory(String id) async =>
      ((await api.get('/api/requests/$id/history')) as List)
          .map((x) => RequestHistoryEntry.fromJson(x))
          .toList();
  Future<void> cancelRequest(String id) => api.delete('/api/requests/$id');

  Future<List<AppNotification>> notifications() async =>
      ((await api.get('/api/notifications/history')) as List)
          .map((x) => AppNotification.fromJson(x))
          .toList();
}
