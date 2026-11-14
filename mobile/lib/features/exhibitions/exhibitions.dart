import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

// ---------------- Enum + nhãn hiển thị ----------------

/// Trạng thái triển lãm, API tính theo ngày + cờ hủy (BR-42, BR-42a).
enum ExhibitionStatus { upcoming, ongoing, finished, cancelled }

ExhibitionStatus exhibitionStatusFromJson(String value) =>
    ExhibitionStatus.values.firstWhere((s) => s.name.toLowerCase() == value.toLowerCase());

extension ExhibitionStatusView on ExhibitionStatus {
  String get label => switch (this) {
        ExhibitionStatus.upcoming => 'Sắp diễn ra',
        ExhibitionStatus.ongoing => 'Đang diễn ra',
        ExhibitionStatus.finished => 'Đã kết thúc',
        ExhibitionStatus.cancelled => 'Đã hủy',
      };

  /// Giá trị gửi lên API khi lọc (`status=Upcoming`).
  String get apiValue => name[0].toUpperCase() + name.substring(1);
}

/// Trạng thái tranh gửi triển lãm (bảng 4.8).
enum EntryStatus { pending, accepted, rejected, sold, unsold, withdrawn }

EntryStatus entryStatusFromJson(String value) =>
    EntryStatus.values.firstWhere((s) => s.name.toLowerCase() == value.toLowerCase());

extension EntryStatusView on EntryStatus {
  String get label => switch (this) {
        EntryStatus.pending => 'Chờ duyệt',
        EntryStatus.accepted => 'Đã duyệt',
        EntryStatus.rejected => 'Bị từ chối',
        EntryStatus.sold => 'Đã bán',
        EntryStatus.unsold => 'Chưa bán',
        EntryStatus.withdrawn => 'Đã rút',
      };

  String get apiValue => name[0].toUpperCase() + name.substring(1);
}

/// Nhãn gộp trạng thái + đã trả tiền: SOLD và đã trả = "Đã bán – đã trả tiền" (PAID trong bảng 4.8).
String entryStatusText(EntryStatus status, bool paid) =>
    status == EntryStatus.sold && paid ? 'Đã bán – đã trả tiền' : status.label;

// ---------------- Hàm thuần ----------------

/// Đọc số tiền người dùng gõ: bỏ mọi ký tự không phải số ("2.000.000" → 2000000); rỗng/0 → null.
double? parseMoney(String text) {
  final digits = text.replaceAll(RegExp(r'[^0-9]'), '');
  if (digits.isEmpty) return null;
  final value = double.parse(digits);
  return value > 0 ? value : null;
}

/// Ngày dạng yyyy-MM-dd để gửi lên API (DateOnly).
String apiDate(DateTime d) =>
    '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// Đọc DateOnly "yyyy-MM-dd" từ API.
DateTime? _date(Object? v) => v == null ? null : DateTime.parse(v as String);

double? _num(Object? v) => (v as num?)?.toDouble();

/// Kiểm tra giá phía client; server vẫn là nơi quyết định cuối cùng (BR-41).
String? validatePrice(String text, {String label = 'Giá'}) =>
    parseMoney(text) == null ? 'Vui lòng nhập $label lớn hơn 0' : null;

/// BR-44: bán dưới giá đề xuất thì cần xác nhận của học viên (ngày + hình thức).
bool needsConsent(double soldPrice, double askingPrice) => soldPrice < askingPrice;

// ---------------- Model ----------------

class ExhibitionSummary {
  const ExhibitionSummary({
    required this.id,
    required this.name,
    required this.venue,
    required this.city,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.daysUntilStart,
    required this.entryCount,
    required this.pendingCount,
  });

  final int id;
  final String name;
  final String venue;
  final String city;
  final DateTime startDate;
  final DateTime endDate;
  final ExhibitionStatus status;
  final int daysUntilStart;
  final int entryCount;
  final int pendingCount;

  factory ExhibitionSummary.fromJson(Map<String, dynamic> json) => ExhibitionSummary(
        id: json['id'] as int,
        name: json['name'] as String,
        venue: json['venue'] as String,
        city: json['city'] as String,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        status: exhibitionStatusFromJson(json['status'] as String),
        daysUntilStart: json['daysUntilStart'] as int,
        entryCount: json['entryCount'] as int,
        pendingCount: json['pendingCount'] as int,
      );
}

/// Chi tiết triển lãm; các cờ `can*` do server tính, app chỉ hiện/ẩn nút.
class ExhibitionDetail {
  const ExhibitionDetail({
    required this.id,
    required this.name,
    required this.venue,
    required this.city,
    required this.organizer,
    required this.description,
    required this.startDate,
    required this.endDate,
    required this.status,
    required this.cancelReason,
    required this.entryCount,
    required this.pendingCount,
    required this.acceptedCount,
    required this.soldCount,
    required this.canEdit,
    required this.canDelete,
    required this.canCancel,
    required this.canFinish,
    required this.canSubmitEntries,
    required this.rowVersion,
  });

  final int id;
  final String name;
  final String venue;
  final String city;
  final String? organizer;
  final String? description;
  final DateTime startDate;
  final DateTime endDate;
  final ExhibitionStatus status;
  final String? cancelReason;
  final int entryCount;
  final int pendingCount;
  final int acceptedCount;
  final int soldCount;
  final bool canEdit;
  final bool canDelete;
  final bool canCancel;
  final bool canFinish;
  final bool canSubmitEntries;
  final String rowVersion;

  factory ExhibitionDetail.fromJson(Map<String, dynamic> json) => ExhibitionDetail(
        id: json['id'] as int,
        name: json['name'] as String,
        venue: json['venue'] as String,
        city: json['city'] as String,
        organizer: json['organizer'] as String?,
        description: json['description'] as String?,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        status: exhibitionStatusFromJson(json['status'] as String),
        cancelReason: json['cancelReason'] as String?,
        entryCount: json['entryCount'] as int,
        pendingCount: json['pendingCount'] as int,
        acceptedCount: json['acceptedCount'] as int,
        soldCount: json['soldCount'] as int,
        canEdit: json['canEdit'] as bool,
        canDelete: json['canDelete'] as bool,
        canCancel: json['canCancel'] as bool,
        canFinish: json['canFinish'] as bool,
        canSubmitEntries: json['canSubmitEntries'] as bool,
        rowVersion: json['rowVersion'] as String,
      );
}

/// Dữ liệu form tạo/sửa triển lãm.
class ExhibitionInput {
  const ExhibitionInput({
    required this.name,
    required this.venue,
    required this.city,
    required this.organizer,
    required this.description,
    required this.startDate,
    required this.endDate,
    this.rowVersion,
  });

  final String name;
  final String venue;
  final String city;
  final String organizer;
  final String description;
  final DateTime startDate;
  final DateTime endDate;
  final String? rowVersion;

  Map<String, dynamic> toJson() => {
        'name': name.trim(),
        'venue': venue.trim(),
        'city': city.trim(),
        'organizer': organizer.trim(),
        'description': description.trim(),
        'startDate': apiDate(startDate),
        'endDate': apiDate(endDate),
        if (rowVersion != null) 'rowVersion': rowVersion,
      };
}

/// Bài đủ điều kiện gửi triển lãm (BEST, cuộc thi đã công bố, chưa có entry hiệu lực).
class EligibleSubmission {
  const EligibleSubmission({
    required this.id,
    required this.title,
    required this.studentName,
    required this.studentCode,
    required this.competitionTitle,
  });

  final int id;
  final String title;
  final String studentName;
  final String studentCode;
  final String competitionTitle;

  factory EligibleSubmission.fromJson(Map<String, dynamic> json) => EligibleSubmission(
        id: json['id'] as int,
        title: json['title'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        competitionTitle: json['competitionTitle'] as String,
      );
}

/// Một dòng trong danh sách tranh triển lãm của Staff.
class EntryListItem {
  const EntryListItem({
    required this.id,
    required this.exhibitionId,
    required this.exhibitionName,
    required this.submissionTitle,
    required this.studentName,
    required this.studentCode,
    required this.askingPrice,
    required this.status,
    required this.paidToStudent,
    required this.soldPrice,
    required this.statusReason,
  });

  final int id;
  final int exhibitionId;
  final String exhibitionName;
  final String submissionTitle;
  final String studentName;
  final String studentCode;
  final double askingPrice;
  final EntryStatus status;
  final bool paidToStudent;
  final double? soldPrice;
  final String? statusReason;

  factory EntryListItem.fromJson(Map<String, dynamic> json) => EntryListItem(
        id: json['id'] as int,
        exhibitionId: json['exhibitionId'] as int,
        exhibitionName: json['exhibitionName'] as String,
        submissionTitle: json['submissionTitle'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        askingPrice: _num(json['askingPrice'])!,
        status: entryStatusFromJson(json['status'] as String),
        paidToStudent: json['paidToStudent'] as bool,
        soldPrice: _num(json['soldPrice']),
        statusReason: json['statusReason'] as String?,
      );
}

/// Chi tiết entry cho Staff kèm cờ thao tác theo bảng chuyển trạng thái 4.8.
class EntryDetail {
  const EntryDetail({
    required this.id,
    required this.exhibitionId,
    required this.exhibitionName,
    required this.exhibitionStatus,
    required this.exhibitionStartDate,
    required this.exhibitionEndDate,
    required this.submissionTitle,
    required this.studentName,
    required this.studentCode,
    required this.competitionTitle,
    required this.askingPrice,
    required this.priceSourceNote,
    required this.status,
    required this.statusReason,
    required this.soldPrice,
    required this.soldDate,
    required this.customerId,
    required this.customerName,
    required this.consentDate,
    required this.consentMethod,
    required this.consentNote,
    required this.paidToStudent,
    required this.paidAmount,
    required this.paidDate,
    required this.paymentMethod,
    required this.paymentReference,
    required this.payDueDate,
    required this.overdueDays,
    required this.canReview,
    required this.canSell,
    required this.isLateSale,
    required this.canAdjustSale,
    required this.adjustLimitedToPayment,
    required this.canPay,
    required this.rowVersion,
  });

  final int id;
  final int exhibitionId;
  final String exhibitionName;
  final ExhibitionStatus exhibitionStatus;
  final DateTime exhibitionStartDate;
  final DateTime exhibitionEndDate;
  final String submissionTitle;
  final String studentName;
  final String studentCode;
  final String competitionTitle;
  final double askingPrice;
  final String? priceSourceNote;
  final EntryStatus status;
  final String? statusReason;
  final double? soldPrice;
  final DateTime? soldDate;
  final int? customerId;
  final String? customerName;
  final DateTime? consentDate;
  final String? consentMethod;
  final String? consentNote;
  final bool paidToStudent;
  final double? paidAmount;
  final DateTime? paidDate;
  final String? paymentMethod;
  final String? paymentReference;
  final DateTime? payDueDate;
  final int overdueDays;
  final bool canReview;
  final bool canSell;
  final bool isLateSale;
  final bool canAdjustSale;
  final bool adjustLimitedToPayment;
  final bool canPay;
  final String rowVersion;

  factory EntryDetail.fromJson(Map<String, dynamic> json) => EntryDetail(
        id: json['id'] as int,
        exhibitionId: json['exhibitionId'] as int,
        exhibitionName: json['exhibitionName'] as String,
        exhibitionStatus: exhibitionStatusFromJson(json['exhibitionStatus'] as String),
        exhibitionStartDate: DateTime.parse(json['exhibitionStartDate'] as String),
        exhibitionEndDate: DateTime.parse(json['exhibitionEndDate'] as String),
        submissionTitle: json['submissionTitle'] as String,
        studentName: json['studentName'] as String,
        studentCode: json['studentCode'] as String,
        competitionTitle: json['competitionTitle'] as String,
        askingPrice: _num(json['askingPrice'])!,
        priceSourceNote: json['priceSourceNote'] as String?,
        status: entryStatusFromJson(json['status'] as String),
        statusReason: json['statusReason'] as String?,
        soldPrice: _num(json['soldPrice']),
        soldDate: _date(json['soldDate']),
        customerId: json['customerId'] as int?,
        customerName: json['customerName'] as String?,
        consentDate: _date(json['consentDate']),
        consentMethod: json['consentMethod'] as String?,
        consentNote: json['consentNote'] as String?,
        paidToStudent: json['paidToStudent'] as bool,
        paidAmount: _num(json['paidAmount']),
        paidDate: _date(json['paidDate']),
        paymentMethod: json['paymentMethod'] as String?,
        paymentReference: json['paymentReference'] as String?,
        payDueDate: _date(json['payDueDate']),
        overdueDays: json['overdueDays'] as int,
        canReview: json['canReview'] as bool,
        canSell: json['canSell'] as bool,
        isLateSale: json['isLateSale'] as bool,
        canAdjustSale: json['canAdjustSale'] as bool,
        adjustLimitedToPayment: json['adjustLimitedToPayment'] as bool,
        canPay: json['canPay'] as bool,
        rowVersion: json['rowVersion'] as String,
      );
}

class Customer {
  const Customer({required this.id, required this.fullName, this.phone, this.email});

  final int id;
  final String fullName;
  final String? phone;
  final String? email;

  factory Customer.fromJson(Map<String, dynamic> json) => Customer(
        id: json['id'] as int,
        fullName: json['fullName'] as String,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
      );
}

/// Tranh đã gửi triển lãm của học viên (F-M09-10).
class StudentEntry {
  const StudentEntry({
    required this.id,
    required this.exhibitionId,
    required this.exhibitionName,
    required this.venue,
    required this.city,
    required this.exhibitionStartDate,
    required this.exhibitionEndDate,
    required this.exhibitionStatus,
    required this.submissionTitle,
    required this.competitionTitle,
    required this.askingPrice,
    required this.status,
    required this.statusReason,
    required this.soldPrice,
    required this.soldDate,
    required this.paidToStudent,
    required this.paidAmount,
    required this.paidDate,
    required this.canEdit,
    required this.rowVersion,
  });

  final int id;
  final int exhibitionId;
  final String exhibitionName;
  final String venue;
  final String city;
  final DateTime exhibitionStartDate;
  final DateTime exhibitionEndDate;
  final ExhibitionStatus exhibitionStatus;
  final String submissionTitle;
  final String competitionTitle;
  final double askingPrice;
  final EntryStatus status;
  final String? statusReason;
  final double? soldPrice;
  final DateTime? soldDate;
  final bool paidToStudent;
  final double? paidAmount;
  final DateTime? paidDate;

  /// Còn PENDING: học viên sửa giá / rút được (BR-46).
  final bool canEdit;
  final String rowVersion;

  factory StudentEntry.fromJson(Map<String, dynamic> json) => StudentEntry(
        id: json['id'] as int,
        exhibitionId: json['exhibitionId'] as int,
        exhibitionName: json['exhibitionName'] as String,
        venue: json['venue'] as String,
        city: json['city'] as String,
        exhibitionStartDate: DateTime.parse(json['exhibitionStartDate'] as String),
        exhibitionEndDate: DateTime.parse(json['exhibitionEndDate'] as String),
        exhibitionStatus: exhibitionStatusFromJson(json['exhibitionStatus'] as String),
        submissionTitle: json['submissionTitle'] as String,
        competitionTitle: json['competitionTitle'] as String,
        askingPrice: _num(json['askingPrice'])!,
        status: entryStatusFromJson(json['status'] as String),
        statusReason: json['statusReason'] as String?,
        soldPrice: _num(json['soldPrice']),
        soldDate: _date(json['soldDate']),
        paidToStudent: json['paidToStudent'] as bool,
        paidAmount: _num(json['paidAmount']),
        paidDate: _date(json['paidDate']),
        canEdit: json['canEdit'] as bool,
        rowVersion: json['rowVersion'] as String,
      );
}

/// Bộ lọc danh sách tranh triển lãm của Staff (F-M09-09). [paid]: null = tất cả.
class EntryFilter {
  const EntryFilter({this.exhibitionId, this.status, this.paid, this.q = ''});

  final int? exhibitionId;
  final EntryStatus? status;
  final bool? paid;
  final String q;

  Map<String, dynamic> toQuery() => {
        if (exhibitionId != null) 'exhibitionId': exhibitionId,
        if (status != null) 'status': status!.apiValue,
        if (paid != null) 'paid': paid,
        if (q.trim().isNotEmpty) 'q': q.trim(),
      };

  @override
  bool operator ==(Object other) =>
      other is EntryFilter &&
      other.exhibitionId == exhibitionId &&
      other.status == status &&
      other.paid == paid &&
      other.q == q;

  @override
  int get hashCode => Object.hash(exhibitionId, status, paid, q);
}

// ---------------- Repository ----------------

class ExhibitionRepository {
  ExhibitionRepository(this._dio);

  final Dio _dio;

  List<T> _list<T>(Response<List<dynamic>> res, T Function(Map<String, dynamic>) from) =>
      res.data!.map((e) => from(e as Map<String, dynamic>)).toList();

  // ----- Học viên -----

  /// GET /api/student/exhibitions
  Future<List<ExhibitionSummary>> studentExhibitions() => guardApi(() async =>
      _list(await _dio.get<List<dynamic>>('/api/student/exhibitions'), ExhibitionSummary.fromJson));

  /// GET /api/student/exhibitions/{id}
  Future<ExhibitionDetail> studentExhibition(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/student/exhibitions/$id');
        return ExhibitionDetail.fromJson(res.data!);
      });

  /// GET /api/student/eligible-submissions
  Future<List<EligibleSubmission>> studentEligible() => guardApi(() async =>
      _list(await _dio.get<List<dynamic>>('/api/student/eligible-submissions'), EligibleSubmission.fromJson));

  /// GET /api/student/exhibition-entries
  Future<List<StudentEntry>> myEntries() => guardApi(() async =>
      _list(await _dio.get<List<dynamic>>('/api/student/exhibition-entries'), StudentEntry.fromJson));

  /// GET /api/student/exhibition-entries/{id}
  Future<StudentEntry> myEntry(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/student/exhibition-entries/$id');
        return StudentEntry.fromJson(res.data!);
      });

  /// POST /api/student/exhibition-entries → entry PENDING.
  Future<StudentEntry> propose({required int exhibitionId, required int submissionId, required double askingPrice}) =>
      guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/student/exhibition-entries',
            data: {'exhibitionId': exhibitionId, 'submissionId': submissionId, 'askingPrice': askingPrice});
        return StudentEntry.fromJson(res.data!);
      });

  /// PUT /api/student/exhibition-entries/{id}: sửa giá khi còn PENDING (kèm RowVersion).
  Future<StudentEntry> updatePrice(int id, double askingPrice, String rowVersion) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('/api/student/exhibition-entries/$id',
            data: {'askingPrice': askingPrice, 'rowVersion': rowVersion});
        return StudentEntry.fromJson(res.data!);
      });

  /// DELETE /api/student/exhibition-entries/{id}: rút đề xuất.
  Future<void> withdraw(int id) =>
      guardApi(() async => _dio.delete<void>('/api/student/exhibition-entries/$id'));

  // ----- Staff: triển lãm -----

  /// GET /api/staff/exhibitions
  Future<List<ExhibitionSummary>> staffExhibitions({String q = '', ExhibitionStatus? status}) => guardApi(() async =>
      _list(
          await _dio.get<List<dynamic>>('/api/staff/exhibitions', queryParameters: {
            if (q.trim().isNotEmpty) 'q': q.trim(),
            if (status != null) 'status': status.apiValue,
          }),
          ExhibitionSummary.fromJson));

  /// GET /api/staff/exhibitions/{id}
  Future<ExhibitionDetail> staffExhibition(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/staff/exhibitions/$id');
        return ExhibitionDetail.fromJson(res.data!);
      });

  /// POST /api/staff/exhibitions
  Future<ExhibitionDetail> createExhibition(ExhibitionInput input) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibitions', data: input.toJson());
        return ExhibitionDetail.fromJson(res.data!);
      });

  /// PUT /api/staff/exhibitions/{id}
  Future<ExhibitionDetail> updateExhibition(int id, ExhibitionInput input) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('/api/staff/exhibitions/$id', data: input.toJson());
        return ExhibitionDetail.fromJson(res.data!);
      });

  /// DELETE /api/staff/exhibitions/{id}
  Future<void> deleteExhibition(int id) =>
      guardApi(() async => _dio.delete<void>('/api/staff/exhibitions/$id'));

  /// POST /api/staff/exhibitions/{id}/cancel
  Future<ExhibitionDetail> cancelExhibition(int id, String reason) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibitions/$id/cancel', data: {'reason': reason});
        return ExhibitionDetail.fromJson(res.data!);
      });

  /// POST /api/staff/exhibitions/{id}/finish: entry ACCEPTED chưa bán → UNSOLD.
  Future<ExhibitionDetail> finishExhibition(int id) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibitions/$id/finish');
        return ExhibitionDetail.fromJson(res.data!);
      });

  // ----- Staff: tranh gửi triển lãm -----

  /// GET /api/staff/exhibition-entries
  Future<List<EntryListItem>> entries(EntryFilter filter) => guardApi(() async => _list(
      await _dio.get<List<dynamic>>('/api/staff/exhibition-entries', queryParameters: filter.toQuery()),
      EntryListItem.fromJson));

  /// GET /api/staff/exhibition-entries/{id}
  Future<EntryDetail> entry(int id) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/staff/exhibition-entries/$id');
        return EntryDetail.fromJson(res.data!);
      });

  /// GET /api/staff/eligible-submissions
  Future<List<EligibleSubmission>> staffEligible(String q) => guardApi(() async => _list(
      await _dio.get<List<dynamic>>('/api/staff/eligible-submissions',
          queryParameters: {if (q.trim().isNotEmpty) 'q': q.trim()}),
      EligibleSubmission.fromJson));

  /// POST /api/staff/exhibitions/{id}/entries: tạo entry ACCEPTED với giá học viên đã báo.
  Future<EntryDetail> createDirectEntry(int exhibitionId,
          {required int submissionId, required double askingPrice, required String priceSourceNote}) =>
      guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibitions/$exhibitionId/entries',
            data: {'submissionId': submissionId, 'askingPrice': askingPrice, 'priceSourceNote': priceSourceNote});
        return EntryDetail.fromJson(res.data!);
      });

  /// POST /api/staff/exhibition-entries/{id}/review
  Future<EntryDetail> review(int id, {required bool accept, String? reason}) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibition-entries/$id/review',
            data: {'decision': accept ? 'Accepted' : 'Rejected', 'reason': ?reason});
        return EntryDetail.fromJson(res.data!);
      });

  /// POST /api/staff/exhibition-entries/{id}/sell
  Future<EntryDetail> sell(int id, Map<String, dynamic> body) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibition-entries/$id/sell', data: body);
        return EntryDetail.fromJson(res.data!);
      });

  /// PUT /api/staff/exhibition-entries/{id}/sale
  Future<EntryDetail> adjustSale(int id, Map<String, dynamic> body) => guardApi(() async {
        final res = await _dio.put<Map<String, dynamic>>('/api/staff/exhibition-entries/$id/sale', data: body);
        return EntryDetail.fromJson(res.data!);
      });

  /// POST /api/staff/exhibition-entries/{id}/pay
  Future<EntryDetail> pay(int id, Map<String, dynamic> body) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/staff/exhibition-entries/$id/pay', data: body);
        return EntryDetail.fromJson(res.data!);
      });

  // ----- Khách mua -----

  /// GET /api/staff/customers
  Future<List<Customer>> customers(String q) => guardApi(() async => _list(
      await _dio.get<List<dynamic>>('/api/staff/customers', queryParameters: {if (q.trim().isNotEmpty) 'q': q.trim()}),
      Customer.fromJson));
}

// ---------------- Provider ----------------

final exhibitionRepositoryProvider = Provider<ExhibitionRepository>((ref) => ExhibitionRepository(ref.watch(dioProvider)));

// Học viên
final studentExhibitionsProvider = FutureProvider<List<ExhibitionSummary>>(
  (ref) => ref.watch(exhibitionRepositoryProvider).studentExhibitions(),
);
final studentExhibitionProvider = FutureProvider.family<ExhibitionDetail, int>(
  (ref, id) => ref.watch(exhibitionRepositoryProvider).studentExhibition(id),
);
final studentEligibleProvider = FutureProvider<List<EligibleSubmission>>(
  (ref) => ref.watch(exhibitionRepositoryProvider).studentEligible(),
);
final myEntriesProvider = FutureProvider<List<StudentEntry>>(
  (ref) => ref.watch(exhibitionRepositoryProvider).myEntries(),
);
final myEntryProvider = FutureProvider.family<StudentEntry, int>(
  (ref, id) => ref.watch(exhibitionRepositoryProvider).myEntry(id),
);

// Staff
final staffExhibitionsProvider = FutureProvider<List<ExhibitionSummary>>(
  (ref) => ref.watch(exhibitionRepositoryProvider).staffExhibitions(),
);
final staffExhibitionProvider = FutureProvider.family<ExhibitionDetail, int>(
  (ref, id) => ref.watch(exhibitionRepositoryProvider).staffExhibition(id),
);
final entriesProvider = FutureProvider.family<List<EntryListItem>, EntryFilter>(
  (ref, filter) => ref.watch(exhibitionRepositoryProvider).entries(filter),
);
final entryDetailProvider = FutureProvider.family<EntryDetail, int>(
  (ref, id) => ref.watch(exhibitionRepositoryProvider).entry(id),
);

/// Làm mới mọi dữ liệu triển lãm/tranh gửi triển lãm sau một thao tác ghi (cả phía Staff lẫn học viên).
/// Dùng [ProviderContainer] để gọi được cả sau khi màn hình đã bị đóng.
void refreshExhibitionData(ProviderContainer container) {
  container.invalidate(studentExhibitionsProvider);
  container.invalidate(studentExhibitionProvider);
  container.invalidate(studentEligibleProvider);
  container.invalidate(myEntriesProvider);
  container.invalidate(myEntryProvider);
  container.invalidate(staffExhibitionsProvider);
  container.invalidate(staffExhibitionProvider);
  container.invalidate(entriesProvider);
  container.invalidate(entryDetailProvider);
}

/// Xóa dữ liệu triển lãm đang giữ khi đổi tài khoản.
void resetExhibitionData(Ref ref) {
  ref.invalidate(studentExhibitionsProvider);
  ref.invalidate(studentExhibitionProvider);
  ref.invalidate(studentEligibleProvider);
  ref.invalidate(myEntriesProvider);
  ref.invalidate(myEntryProvider);
  ref.invalidate(staffExhibitionsProvider);
  ref.invalidate(staffExhibitionProvider);
  ref.invalidate(entriesProvider);
  ref.invalidate(entryDetailProvider);
}
