import 'package:fine_arts_mobile/features/exhibitions/exhibitions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('parseMoney', () {
    test('bỏ dấu phân cách, chỉ lấy chữ số', () {
      expect(parseMoney('2.000.000'), 2000000);
      expect(parseMoney(' 1500000 ₫'), 1500000);
    });

    test('rỗng hoặc 0 → null (giá phải > 0, BR-41)', () {
      expect(parseMoney(''), isNull);
      expect(parseMoney('0'), isNull);
      expect(parseMoney('abc'), isNull);
    });
  });

  test('validatePrice báo lỗi khi giá không hợp lệ', () {
    expect(validatePrice('', label: 'giá đề xuất'), 'Vui lòng nhập giá đề xuất lớn hơn 0');
    expect(validatePrice('2000000'), isNull);
  });

  test('apiDate dạng yyyy-MM-dd', () {
    expect(apiDate(DateTime(2026, 7, 5)), '2026-07-05');
  });

  test('needsConsent: chỉ khi bán thấp hơn giá đề xuất (BR-44)', () {
    expect(needsConsent(1500000, 2000000), isTrue);
    expect(needsConsent(2000000, 2000000), isFalse);
    expect(needsConsent(2500000, 2000000), isFalse);
  });

  test('entryStatusText: SOLD + đã trả = "Đã bán – đã trả tiền"', () {
    expect(entryStatusText(EntryStatus.sold, true), 'Đã bán – đã trả tiền');
    expect(entryStatusText(EntryStatus.sold, false), 'Đã bán');
    expect(entryStatusText(EntryStatus.pending, false), 'Chờ duyệt');
  });

  test('enum đọc từ API không phân biệt hoa thường', () {
    expect(entryStatusFromJson('Accepted'), EntryStatus.accepted);
    expect(exhibitionStatusFromJson('Cancelled'), ExhibitionStatus.cancelled);
    expect(EntryStatus.unsold.apiValue, 'Unsold');
  });

  group('EntryFilter', () {
    test('toQuery chỉ gửi điều kiện đang bật', () {
      expect(const EntryFilter().toQuery(), isEmpty);
      final q = const EntryFilter(exhibitionId: 3, status: EntryStatus.sold, paid: false, q: ' Lan ').toQuery();
      expect(q, {'exhibitionId': 3, 'status': 'Sold', 'paid': false, 'q': 'Lan'});
    });

    test('so sánh theo giá trị để làm khóa provider', () {
      expect(const EntryFilter(status: EntryStatus.sold), const EntryFilter(status: EntryStatus.sold));
      expect(const EntryFilter(status: EntryStatus.sold), isNot(const EntryFilter(status: EntryStatus.pending)));
    });
  });

  test('ExhibitionInput.toJson cắt khoảng trắng, gửi ngày DateOnly và RowVersion khi sửa', () {
    final json = ExhibitionInput(
      name: ' Triển lãm A ',
      venue: 'Phòng 1',
      city: 'Hà Nội',
      organizer: '',
      description: '',
      startDate: DateTime(2026, 8, 1),
      endDate: DateTime(2026, 8, 8),
      rowVersion: 'AAA=',
    ).toJson();
    expect(json['name'], 'Triển lãm A');
    expect(json['startDate'], '2026-08-01');
    expect(json['rowVersion'], 'AAA=');
  });

  test('StudentEntry.fromJson đọc giá, ngày bán và trạng thái trả tiền', () {
    final e = StudentEntry.fromJson({
      'id': 5,
      'exhibitionId': 2,
      'exhibitionName': 'TL',
      'venue': 'V',
      'city': 'C',
      'exhibitionStartDate': '2026-08-01',
      'exhibitionEndDate': '2026-08-08',
      'exhibitionStatus': 'Ongoing',
      'submissionId': 9,
      'submissionTitle': 'Biển lặng',
      'competitionTitle': 'Mùa hè xanh',
      'askingPrice': 2000000.0,
      'status': 'Sold',
      'statusReason': null,
      'soldPrice': 2500000,
      'soldDate': '2026-08-03',
      'paidToStudent': true,
      'paidAmount': 2500000,
      'paidDate': '2026-08-05',
      'canEdit': false,
      'rowVersion': 'AAA=',
    });
    expect(e.status, EntryStatus.sold);
    expect(e.soldPrice, 2500000);
    expect(e.paidDate, DateTime(2026, 8, 5));
    expect(e.exhibitionStatus, ExhibitionStatus.ongoing);
  });
}
