using LifeLink.Domain.Entities;
using LifeLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LifeLink.Infrastructure.Persistence;

/// <summary>
/// Island-wide demo data so searches and maps find people everywhere, not just in ten cities: for each of Sri Lanka's
/// 25 districts, eight donors, two real hospitals with a staff login, a blood centre with stock, a coordinator with an
/// upcoming camp (09:00-15:00 Sri Lanka time, locally booked) and one open blood request.
/// Added once: it skips itself when the first district's donor already exists. Demo accounts share one password.
/// </summary>
public static class SriLankaDemoData
{
    public sealed record Road(string Street, string Town);
    public sealed record HospitalSeed(string Name, decimal Lat, decimal Lon);
    public sealed record District(string Key, string Name, string Province, decimal Lat, decimal Lon, Road[] Roads, HospitalSeed[] Hospitals);

    public static readonly IReadOnlyList<District> Districts =
    [
        new("colombo", "Colombo", "Western", 6.9271m, 79.8612m, [new("Galle Road", "Wellawatte"), new("Havelock Road", "Colombo 05"), new("Nawala Road", "Rajagiriya"), new("High Level Road", "Maharagama")],
            [new("Colombo South Teaching Hospital, Kalubowila", 6.8667m, 79.8770m), new("Sri Jayewardenepura General Hospital", 6.8697m, 79.9200m)]),
        new("gampaha", "Gampaha", "Western", 7.0840m, 80.0098m, [new("Colombo Road", "Gampaha"), new("Negombo Road", "Ja-Ela"), new("Kandy Road", "Kadawatha"), new("Main Street", "Negombo")],
            [new("District General Hospital Gampaha", 7.0917m, 79.9997m), new("Colombo North Teaching Hospital, Ragama", 7.0300m, 79.9206m)]),
        new("kalutara", "Kalutara", "Western", 6.5854m, 79.9607m, [new("Galle Road", "Panadura"), new("Station Road", "Kalutara"), new("Horana Road", "Bandaragama"), new("Main Street", "Beruwala")],
            [new("District General Hospital Kalutara", 6.5831m, 79.9594m), new("Base Hospital Horana", 6.7159m, 80.0626m)]),
        new("kandy", "Kandy", "Central", 7.2906m, 80.6337m, [new("Peradeniya Road", "Kandy"), new("Katugastota Road", "Kandy"), new("Main Street", "Gampola"), new("Digana Road", "Kundasale")],
            [new("Teaching Hospital Peradeniya", 7.2690m, 80.5960m), new("District General Hospital Gampola", 7.1640m, 80.5770m)]),
        new("matale", "Matale", "Central", 7.4675m, 80.6234m, [new("Trincomalee Street", "Matale"), new("Kandy Road", "Ukuwela"), new("Main Street", "Dambulla"), new("Rattota Road", "Matale")],
            [new("District General Hospital Matale", 7.4700m, 80.6230m), new("Base Hospital Dambulla", 7.8600m, 80.6517m)]),
        new("nuwaraeliya", "Nuwara Eliya", "Central", 6.9497m, 80.7891m, [new("Badulla Road", "Nuwara Eliya"), new("Main Street", "Hatton"), new("Uda Pussellawa Road", "Nuwara Eliya"), new("Talawakele Road", "Kotagala")],
            [new("District General Hospital Nuwara Eliya", 6.9690m, 80.7670m), new("Base Hospital Dickoya", 6.8800m, 80.6100m)]),
        new("galle", "Galle", "Southern", 6.0535m, 80.2210m, [new("Wakwella Road", "Galle"), new("Matara Road", "Unawatuna"), new("Main Street", "Ambalangoda"), new("Colombo Road", "Hikkaduwa")],
            [new("Teaching Hospital Mahamodara", 6.0400m, 80.2050m), new("Base Hospital Balapitiya", 6.2690m, 80.0380m)]),
        new("matara", "Matara", "Southern", 5.9549m, 80.5550m, [new("Anagarika Dharmapala Mawatha", "Matara"), new("Akuressa Road", "Matara"), new("Main Street", "Weligama"), new("Tangalle Road", "Dickwella")],
            [new("Base Hospital Kamburupitiya", 6.0750m, 80.5650m), new("Base Hospital Deniyaya", 6.3420m, 80.5590m)]),
        new("hambantota", "Hambantota", "Southern", 6.1241m, 81.1185m, [new("Tissa Road", "Hambantota"), new("Main Street", "Tangalle"), new("Kataragama Road", "Tissamaharama"), new("Main Street", "Ambalantota")],
            [new("District General Hospital Hambantota", 6.1240m, 81.1100m), new("Base Hospital Tangalle", 6.0240m, 80.7940m)]),
        new("jaffna", "Jaffna", "Northern", 9.6615m, 80.0255m, [new("Hospital Road", "Jaffna"), new("KKS Road", "Kokuvil"), new("Point Pedro Road", "Nallur"), new("Palaly Road", "Thirunelveli")],
            [new("Base Hospital Point Pedro", 9.8170m, 80.2330m), new("Base Hospital Chavakachcheri", 9.6580m, 80.1630m)]),
        new("kilinochchi", "Kilinochchi", "Northern", 9.3803m, 80.3770m, [new("A9 Road", "Kilinochchi"), new("Paranthan Road", "Paranthan"), new("Main Street", "Kilinochchi"), new("Akkarayan Road", "Kilinochchi")],
            [new("District General Hospital Kilinochchi", 9.3960m, 80.4000m), new("Base Hospital Tharmapuram", 9.3700m, 80.5300m)]),
        new("mannar", "Mannar", "Northern", 8.9810m, 79.9044m, [new("Main Street", "Mannar"), new("Talaimannar Road", "Pesalai"), new("Medawachchiya Road", "Murunkan"), new("Station Road", "Mannar")],
            [new("District General Hospital Mannar", 8.9770m, 79.9100m), new("Base Hospital Murunkan", 8.8300m, 80.0500m)]),
        new("vavuniya", "Vavuniya", "Northern", 8.7514m, 80.4971m, [new("Kandy Road", "Vavuniya"), new("Mannar Road", "Vavuniya"), new("Horowpothana Road", "Vavuniya"), new("Station Road", "Vavuniya")],
            [new("District General Hospital Vavuniya", 8.7540m, 80.4980m), new("Base Hospital Cheddikulam", 8.6600m, 80.3100m)]),
        new("mullaitivu", "Mullaitivu", "Northern", 9.2671m, 80.8142m, [new("Main Street", "Mullaitivu"), new("Puthukudiyiruppu Road", "Puthukudiyiruppu"), new("Oddusuddan Road", "Oddusuddan"), new("Mankulam Road", "Mallavi")],
            [new("District General Hospital Mullaitivu", 9.2670m, 80.8140m), new("Base Hospital Mallavi", 9.1300m, 80.3000m)]),
        new("batticaloa", "Batticaloa", "Eastern", 7.7310m, 81.6747m, [new("Trinco Road", "Batticaloa"), new("Bar Road", "Batticaloa"), new("Main Street", "Kattankudy"), new("Kalmunai Road", "Eravur")],
            [new("Base Hospital Kaluwanchikudy", 7.5180m, 81.7820m), new("Base Hospital Valaichchenai", 7.9230m, 81.5300m)]),
        new("ampara", "Ampara", "Eastern", 7.2975m, 81.6820m, [new("D.S. Senanayake Street", "Ampara"), new("Main Street", "Kalmunai"), new("Akkaraipattu Road", "Akkaraipattu"), new("Inginiyagala Road", "Ampara")],
            [new("District General Hospital Ampara", 7.2910m, 81.6720m), new("Ashraff Memorial Hospital Kalmunai", 7.4100m, 81.8300m)]),
        new("trincomalee", "Trincomalee", "Eastern", 8.5874m, 81.2152m, [new("Dockyard Road", "Trincomalee"), new("Kandy Road", "Trincomalee"), new("Main Street", "Kinniya"), new("Kantale Road", "Kantale")],
            [new("District General Hospital Trincomalee", 8.5700m, 81.2330m), new("Base Hospital Kantale", 8.3600m, 81.0000m)]),
        new("kurunegala", "Kurunegala", "North Western", 7.4863m, 80.3623m, [new("Colombo Road", "Kurunegala"), new("Puttalam Road", "Kurunegala"), new("Main Street", "Kuliyapitiya"), new("Negombo Road", "Pannala")],
            [new("Base Hospital Kuliyapitiya", 7.4690m, 80.0400m), new("Base Hospital Nikaweratiya", 7.7500m, 80.1200m)]),
        new("puttalam", "Puttalam", "North Western", 8.0362m, 79.8283m, [new("Colombo Road", "Puttalam"), new("Main Street", "Chilaw"), new("Kurunegala Road", "Wennappuwa"), new("Anuradhapura Road", "Puttalam")],
            [new("District General Hospital Chilaw", 7.5750m, 79.7950m), new("Base Hospital Puttalam", 8.0330m, 79.8260m)]),
        new("anuradhapura", "Anuradhapura", "North Central", 8.3114m, 80.4037m, [new("Maithripala Senanayake Mawatha", "Anuradhapura"), new("Jaffna Road", "Anuradhapura"), new("Main Street", "Kekirawa"), new("Kurunegala Road", "Thambuttegama")],
            [new("Base Hospital Thambuttegama", 8.1500m, 80.3000m), new("Base Hospital Kebithigollewa", 8.5300m, 80.6700m)]),
        new("polonnaruwa", "Polonnaruwa", "North Central", 7.9403m, 81.0188m, [new("Batticaloa Road", "Polonnaruwa"), new("Habarana Road", "Polonnaruwa"), new("Main Street", "Hingurakgoda"), new("Manampitiya Road", "Kaduruwela")],
            [new("District General Hospital Polonnaruwa", 7.9400m, 81.0000m), new("Base Hospital Medirigiriya", 8.1450m, 80.9640m)]),
        new("badulla", "Badulla", "Uva", 6.9934m, 81.0550m, [new("Lower Street", "Badulla"), new("Bandarawela Road", "Badulla"), new("Main Street", "Bandarawela"), new("Nuwara Eliya Road", "Welimada")],
            [new("Base Hospital Diyatalawa", 6.8170m, 80.9670m), new("Base Hospital Mahiyanganaya", 7.3200m, 80.9900m)]),
        new("monaragala", "Monaragala", "Uva", 6.8728m, 81.3507m, [new("Wellawaya Road", "Monaragala"), new("Main Street", "Bibile"), new("Siyambalanduwa Road", "Monaragala"), new("Main Street", "Buttala")],
            [new("District General Hospital Monaragala", 6.8720m, 81.3500m), new("Base Hospital Wellawaya", 6.7380m, 81.1020m)]),
        new("ratnapura", "Ratnapura", "Sabaragamuwa", 6.6828m, 80.3992m, [new("Main Street", "Ratnapura"), new("Colombo Road", "Pelmadulla"), new("Main Street", "Balangoda"), new("Embilipitiya Road", "Godakawela")],
            [new("Base Hospital Balangoda", 6.6500m, 80.7000m), new("District General Hospital Embilipitiya", 6.3430m, 80.8500m)]),
        new("kegalle", "Kegalle", "Sabaragamuwa", 7.2513m, 80.3464m, [new("Kandy Road", "Kegalle"), new("Main Street", "Mawanella"), new("Avissawella Road", "Ruwanwella"), new("Bulathkohupitiya Road", "Kegalle")],
            [new("District General Hospital Kegalle", 7.2510m, 80.3460m), new("Base Hospital Mawanella", 7.2530m, 80.4450m)]),
    ];

    /// <summary>Roughly Sri Lanka's ABO/Rh mix (O+ ≈ 40%, B+ ≈ 30%, A+ ≈ 22%); the eighth donor rotates through the rare negative types.</summary>
    private static readonly BloodType[] TypeMix = [BloodType.OPositive, BloodType.BPositive, BloodType.APositive, BloodType.OPositive, BloodType.BPositive, BloodType.APositive, BloodType.ABPositive];
    private static readonly BloodType[] RareTypes = [BloodType.ONegative, BloodType.ANegative, BloodType.BNegative, BloodType.ABNegative];
    private static readonly string[] Venues = ["Town Hall", "Central College Auditorium", "Public Library Hall", "Divisional Secretariat Hall", "Community Centre"];
    private static readonly string[] Positions = ["Blood Bank Medical Officer", "Nursing Officer", "Consultant Haematologist", "Registrar"];
    private static readonly string[] RequestNotes =
    [
        "Road traffic accident admitted to the emergency unit.", "Elective surgery scheduled for tomorrow morning.", "Dengue patient with falling platelets.",
        "Post-partum haemorrhage in the maternity ward.", "Thalassaemia patient due for routine transfusion.",
    ];

    public static async Task<bool> ApplyAsync(LifeLinkDbContext db, string passwordHash, DateTimeOffset now, CancellationToken ct)
    {
        var marker = $"donor.{Districts[0].Key}.01@{DemoDataSeeder.EmailDomain}";
        if (await db.Users.AnyAsync(x => x.Email == marker, ct)) return false;

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var existingCentres = (await db.BloodBankLocations.Select(x => x.Name).ToListAsync(ct)).Select(x => x.Trim().ToLowerInvariant()).ToHashSet();
        User NewUser(string local, UserRole role) { var u = new User($"{local}@{DemoDataSeeder.EmailDomain}", passwordHash, role); db.Users.Add(u); return u; }

        for (var d = 0; d < Districts.Count; d++)
        {
            var district = Districts[d];

            // Hospitals with one staff login each; one in five starts out awaiting verification.
            var hospitals = new List<(Hospital Hospital, User Staff)>();
            for (var h = 0; h < district.Hospitals.Length; h++)
            {
                var seed = district.Hospitals[h];
                var hospital = new Hospital(seed.Name, $"DEMO-LK-{district.Key.ToUpperInvariant()}-{h + 1}", $"{seed.Name}, {district.Name} District", seed.Lat, seed.Lon);
                hospital.SetVerification((d * 2 + h) % 5 == 4 ? VerificationStatus.Pending : VerificationStatus.Verified);
                db.Hospitals.Add(hospital);
                var staff = NewUser($"hospital.{district.Key}.{h + 1}", UserRole.HospitalRequester);
                db.HospitalStaff.Add(new HospitalStaff(staff.Id, hospital.Id, Positions[(d + h) % Positions.Length]));
                hospitals.Add((hospital, staff));
            }

            // A blood centre per district (unless one already exists) with six lots across common and rare types.
            var centreName = $"Regional Blood Centre {district.Name}";
            if (existingCentres.Add(centreName.ToLowerInvariant()))
            {
                var centre = new BloodBankLocation(centreName, $"{district.Roads[0].Street}, {district.Roads[0].Town}", district.Lat + 0.006m, district.Lon + 0.004m);
                db.BloodBankLocations.Add(centre);
                BloodType[] stock = [BloodType.OPositive, BloodType.BPositive, BloodType.APositive, BloodType.ONegative, BloodType.ABPositive, RareTypes[(d + 1) % RareTypes.Length]];
                for (var s = 0; s < stock.Length; s++)
                    db.InventoryLots.Add(new InventoryLot(centre.Id, stock[s], 4 + (d * 3 + s * 5) % 17, today.AddDays(3 + (d * 7 + s * 11) % 36), s % 2 == 0 ? "Walk-in donations" : $"{district.Name} mobile donation camp"));
            }

            // Eight donors around the district: six eligible, one awaiting verification, one in the 90-day wait.
            var eligible = new List<Donor>();
            for (var j = 0; j < 8; j++)
            {
                var user = NewUser($"donor.{district.Key}.{j + 1:00}", UserRole.Donor);
                var road = district.Roads[j % district.Roads.Length];
                var type = j == 7 ? RareTypes[d % RareTypes.Length] : TypeMix[(j + d) % TypeMix.Length];
                var dob = new DateOnly(1968 + (d * 7 + j * 5) % 37, 1 + (d + j) % 12, 1 + (d * 3 + j * 7) % 28);
                var donor = new Donor(user.Id, type, dob, $"No. {12 + (d * 13 + j * 29) % 280}, {road.Street}, {road.Town}",
                    district.Lat + ((j * 7 % 11) - 5) * 0.004m, district.Lon + ((j * 5 % 9) - 4) * 0.004m, j == 6 && d % 3 == 0 ? """["Recent dental extraction"]""" : "[]");
                db.Donors.Add(donor);
                var status = j switch { 5 => EligibilityStatus.PendingVerification, 6 => EligibilityStatus.TemporarilyIneligible, _ => EligibilityStatus.Eligible };
                if (j != 5)
                {
                    // Previous donations at the district centre; donor 7 gave blood a month ago, so must wait 90 days.
                    var visits = j == 6 ? 1 : 1 + (d + j) % 3;
                    for (var v = 0; v < visits; v++)
                    {
                        var date = j == 6 ? today.AddDays(-30) : today.AddDays(-(100 + (d * 11 + j * 17) % 120 + v * 140));
                        db.DonationRecords.Add(new DonationRecord(donor.Id, date, 1, centreName, v == 0 ? null : "Routine donation"));
                        if (v == 0) donor.RecordDonation(date);
                    }
                    db.DonorEligibilityHistory.Add(new DonorEligibilityHistory(donor.Id, EligibilityStatus.PendingVerification, status,
                        status == EligibilityStatus.Eligible ? "All deterministic eligibility checks passed." : "A 90-day interval is required after the previous donation.", null));
                    donor.ApplyEligibility(status);
                }
                if (status == EligibilityStatus.Eligible) eligible.Add(donor);
            }

            // An upcoming camp, 09:00-15:00 Sri Lanka time (03:30-09:30 UTC), run by the district's coordinator.
            var coordinator = NewUser($"coordinator.{district.Key}", UserRole.CampCoordinator);
            var venueRoad = district.Roads[d % district.Roads.Length];
            var starts = new DateTimeOffset(today.AddDays(3 + d * 5 % 40).ToDateTime(new TimeOnly(3, 30)), TimeSpan.Zero);
            var camp = new DonationCamp(coordinator.Id, $"{district.Name} Community Blood Drive", $"{venueRoad.Town} {Venues[d % Venues.Length]}, {venueRoad.Street}",
                district.Lat - 0.003m, district.Lon + 0.003m, starts, starts.AddHours(6), 12);
            camp.TransitionTo(CampStatus.Scheduled);
            db.DonationCamps.Add(camp);
            for (var s = 0; s < 12; s++)
            {
                var slot = new CampSlot(camp.Id, starts.AddMinutes(s * 20));
                if (s % 2 == 0 && s / 2 < Math.Min(4, eligible.Count)) slot.Book(eligible[s / 2].Id);
                db.CampSlots.Add(slot);
            }

            // One open request from the district's main hospital (left unprocessed, so an admin can start the agents).
            var (mainHospital, mainStaff) = hospitals[0];
            if (mainHospital.VerificationStatus == VerificationStatus.Verified)
            {
                var urgency = (RequestUrgency)(d % 3);
                db.BloodRequests.Add(new BloodRequest(mainHospital.Id, mainStaff.Id, TypeMix[d % TypeMix.Length], 1 + d % 4, urgency, RequestNotes[d % RequestNotes.Length],
                    now.AddHours(urgency switch { RequestUrgency.Critical => 8, RequestUrgency.Urgent => 30, _ => 96 })));
            }
            else
            {
                var (other, otherStaff) = hospitals[1];
                db.BloodRequests.Add(new BloodRequest(other.Id, otherStaff.Id, TypeMix[d % TypeMix.Length], 2, RequestUrgency.Routine, RequestNotes[d % RequestNotes.Length], now.AddHours(96)));
            }
        }
        await db.SaveChangesAsync(ct);
        return true;
    }
}
