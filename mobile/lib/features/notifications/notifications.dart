import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

/// One notification of the signed-in user (same list as the bell of the website).
class AppNotification {
  const AppNotification({
    required this.id,
    required this.title,
    required this.message,
    required this.link,
    required this.isRead,
    required this.createdAt,
  });

  final int id;
  final String title;
  final String message;

  /// Link of the website (e.g. /student/results); see [appRouteFor] for the screen it opens in the app.
  final String? link;
  final bool isRead;
  final DateTime createdAt;

  AppNotification copyWith({bool? isRead}) => AppNotification(
        id: id,
        title: title,
        message: message,
        link: link,
        isRead: isRead ?? this.isRead,
        createdAt: createdAt,
      );

  factory AppNotification.fromJson(Map<String, dynamic> json) => AppNotification(
        id: json['id'] as int,
        title: json['title'] as String,
        message: json['message'] as String,
        link: json['link'] as String?,
        isRead: json['isRead'] as bool,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

/// A page of notifications and the unread count.
class NotificationPage {
  const NotificationPage({required this.items, required this.page, required this.totalPages, required this.unreadCount});

  final List<AppNotification> items;
  final int page;
  final int totalPages;
  final int unreadCount;

  bool get hasMore => page < totalPages;

  factory NotificationPage.fromJson(Map<String, dynamic> json) => NotificationPage(
        items: (json['items'] as List).map((e) => AppNotification.fromJson(e as Map<String, dynamic>)).toList(),
        page: json['page'] as int,
        totalPages: json['totalPages'] as int,
        unreadCount: json['unreadCount'] as int,
      );
}

/// Screen of the app for a notification's website link, or null when the app has no such screen (the notification
/// is then only marked as read). Results and awards are shown with the student's entries.
String? appRouteFor(String? link) {
  if (link == null || link.isEmpty) return null;
  final path = Uri.tryParse(link)?.path ?? link;
  if (path == '/student/results' || path == '/student/awards') return '/student/entries';
  final competition = RegExp(r'^/student/competitions/(\d+)$').firstMatch(path);
  if (competition != null) return '/competitions/${competition.group(1)}';
  const sameInApp = [
    r'^/student/exhibition-entries/\d+$',
    r'^/student/exhibitions/\d+$',
    r'^/student/submissions/\d+$',
    r'^/staff/exhibition-entries/\d+$',
    r'^/staff/exhibitions/\d+$',
    r'^/(student|staff|teacher)$',
  ];
  return sameInApp.any((p) => RegExp(p).hasMatch(path)) ? path : null;
}

/// Notifications API (Controllers/Api/NotificationsApiController.cs) and the phone's push registration.
class NotificationRepository {
  NotificationRepository(this._dio);

  final Dio _dio;

  Future<NotificationPage> page(int page) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/notifications', queryParameters: {'page': page});
        return NotificationPage.fromJson(res.data!);
      });

  Future<int> unreadCount() => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/notifications/unread-count');
        return res.data!['unreadCount'] as int;
      });

  /// Marks one as read; returns the unread count left.
  Future<int> markRead(int id) => guardApi(() async {
        final res = await _dio.post<Map<String, dynamic>>('/api/notifications/$id/read');
        return res.data!['unreadCount'] as int;
      });

  Future<void> markAllRead() => guardApi(() async {
        await _dio.post<void>('/api/notifications/read-all');
      });

  Future<void> registerDevice(String token, String platform) => guardApi(() async {
        await _dio.post<void>('/api/devices', data: {'token': token, 'platform': platform});
      });

  Future<void> unregisterDevice(String token) => guardApi(() async {
        await _dio.delete<void>('/api/devices', data: {'token': token});
      });
}

final notificationRepositoryProvider =
    Provider<NotificationRepository>((ref) => NotificationRepository(ref.watch(dioProvider)));

/// Unread count for the bell's badge; refreshed when a push arrives and after reading.
final unreadCountProvider = FutureProvider<int>((ref) => ref.watch(notificationRepositoryProvider).unreadCount());

/// The notification list, page by page ("load more" at the end).
final notificationListProvider =
    AsyncNotifierProvider<NotificationListController, NotificationPage>(NotificationListController.new);

class NotificationListController extends AsyncNotifier<NotificationPage> {
  bool _loadingMore = false;

  @override
  Future<NotificationPage> build() => ref.watch(notificationRepositoryProvider).page(1);

  /// Next page appended to the list.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || !current.hasMore || _loadingMore) return;
    _loadingMore = true;
    try {
      final next = await ref.read(notificationRepositoryProvider).page(current.page + 1);
      state = AsyncData(NotificationPage(
        items: [...current.items, ...next.items],
        page: next.page,
        totalPages: next.totalPages,
        unreadCount: next.unreadCount,
      ));
    } finally {
      _loadingMore = false;
    }
  }

  /// Marks one as read at once on screen, then on the server (put back if that fails).
  Future<void> markRead(AppNotification n) async {
    if (n.isRead) return;
    final before = state.value;
    if (before != null) {
      state = AsyncData(NotificationPage(
        items: [for (final x in before.items) x.id == n.id ? x.copyWith(isRead: true) : x],
        page: before.page,
        totalPages: before.totalPages,
        unreadCount: before.unreadCount > 0 ? before.unreadCount - 1 : 0,
      ));
    }
    try {
      await ref.read(notificationRepositoryProvider).markRead(n.id);
    } catch (_) {
      if (before != null) state = AsyncData(before);
      rethrow;
    } finally {
      ref.invalidate(unreadCountProvider);
    }
  }

  /// Marks every notification as read (same optimistic update).
  Future<void> markAllRead() async {
    final before = state.value;
    if (before != null) {
      state = AsyncData(NotificationPage(
        items: [for (final x in before.items) x.copyWith(isRead: true)],
        page: before.page,
        totalPages: before.totalPages,
        unreadCount: 0,
      ));
    }
    try {
      await ref.read(notificationRepositoryProvider).markAllRead();
    } catch (_) {
      if (before != null) state = AsyncData(before);
      rethrow;
    } finally {
      ref.invalidate(unreadCountProvider);
    }
  }
}

/// Drops the notifications of the previous account (sign out / another user).
void resetNotificationData(Ref ref) {
  ref.invalidate(unreadCountProvider);
  ref.invalidate(notificationListProvider);
}
