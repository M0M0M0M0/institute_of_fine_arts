import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../app/tokens.dart';
import '../../core/api_client.dart';
import '../../core/async_view.dart';
import '../../core/format.dart';
import '../../shared/shared.dart';
import '../auth/auth.dart';
import 'notifications.dart';

/// Bell for the app bar of a signed-in user: opens the notification list, a badge shows the unread count.
/// Nothing for guests.
class NotificationBell extends ConsumerWidget {
  const NotificationBell({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    if (currentUserOf(ref.watch(authControllerProvider)) == null) return const SizedBox.shrink();
    final unread = ref.watch(unreadCountProvider).value ?? 0;
    final label = unread == 0 ? 'Notifications' : 'Notifications, $unread unread';
    return IconButton(
      tooltip: label,
      onPressed: () => context.push('/notifications'),
      icon: Badge(
        isLabelVisible: unread > 0,
        label: Text(unread > 99 ? '99+' : '$unread'),
        backgroundColor: AppColors.danger,
        child: Icon(unread > 0 ? Icons.notifications : Icons.notifications_outlined, semanticLabel: label),
      ),
    );
  }
}

/// All notifications of the signed-in user, newest first (unread ones marked with a brass dot). Tapping one marks it
/// as read and opens its screen when the app has one; "Mark all read" in the bar; pull to refresh; more on scroll.
class NotificationsScreen extends ConsumerWidget {
  const NotificationsScreen({super.key});

  Future<void> _open(BuildContext context, WidgetRef ref, AppNotification n) async {
    final route = appRouteFor(n.link);
    try {
      await ref.read(notificationListProvider.notifier).markRead(n);
    } catch (e) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ApiException.from(e).message)));
      }
      return;
    }
    if (route != null && context.mounted) context.push(route);
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final list = ref.watch(notificationListProvider);
    final hasUnread = (list.value?.unreadCount ?? 0) > 0;
    final gutter = AppLayout.gutterFor(MediaQuery.sizeOf(context).width);
    return Scaffold(
      appBar: AppBar(
        leading: const AppBackButton(),
        leadingWidth: 48,
        titleSpacing: 0,
        title: const Text('Notifications'),
        actions: [
          if (hasUnread)
            TextButton(
              onPressed: () async {
                try {
                  await ref.read(notificationListProvider.notifier).markAllRead();
                } catch (e) {
                  if (context.mounted) {
                    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ApiException.from(e).message)));
                  }
                }
              },
              child: const Text('Mark all read'),
            ),
        ],
      ),
      body: AsyncValueView(
        value: list,
        onRetry: () => ref.invalidate(notificationListProvider),
        placeholder: _placeholder,
        data: (page) => RefreshIndicator(
          onRefresh: () async {
            ref.invalidate(unreadCountProvider);
            ref.invalidate(notificationListProvider);
            await ref.read(notificationListProvider.future);
          },
          child: page.items.isEmpty
              ? ListView(
                  physics: const AlwaysScrollableScrollPhysics(),
                  children: const [
                    SizedBox(height: AppSpacing.xxl),
                    EmptyState(
                      icon: Icons.notifications_none,
                      title: 'No notifications yet',
                      message: 'News about your competitions, awards and exhibitions shows up here.',
                    ),
                  ],
                )
              : NotificationListener<ScrollNotification>(
                  onNotification: (n) {
                    if (n.metrics.extentAfter < 400) ref.read(notificationListProvider.notifier).loadMore();
                    return false;
                  },
                  child: ListView.separated(
                    physics: const AlwaysScrollableScrollPhysics(),
                    padding: EdgeInsets.fromLTRB(gutter, AppSpacing.sm, gutter, AppSpacing.xl),
                    itemCount: page.items.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (context, i) => StaggerIn(
                      index: i,
                      child: _NotificationTile(item: page.items[i], onTap: () => _open(context, ref, page.items[i])),
                    ),
                  ),
                ),
        ),
      ),
    );
  }
}

/// Stand-in list for the loading skeleton.
final _placeholder = NotificationPage(
  items: [
    for (var i = 0; i < 6; i++)
      AppNotification(
        id: -1 - i,
        title: 'Results are published',
        message: 'The results of Spring Landscapes are published. Open the competition to see the winners.',
        link: null,
        isRead: i > 1,
        createdAt: DateTime(2026, 1, 1),
      ),
  ],
  page: 1,
  totalPages: 1,
  unreadCount: 2,
);

/// One notification: unread dot, title (bold while unread), message, time.
class _NotificationTile extends StatelessWidget {
  const _NotificationTile({required this.item, required this.onTap});

  final AppNotification item;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final text = Theme.of(context).textTheme;
    return Semantics(
      label: item.isRead ? null : 'Unread',
      child: Pressable(
        pressedScale: 0.98,
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: AppSpacing.md),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // A brass dot marks what is new; read ones keep the same indent
              Padding(
                padding: const EdgeInsets.only(top: 7, right: AppSpacing.md),
                child: SizedBox.square(
                  dimension: 8,
                  child: item.isRead ? null : const DecoratedBox(
                    decoration: BoxDecoration(color: AppColors.brass, shape: BoxShape.circle),
                  ),
                ),
              ),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      item.title,
                      style: text.titleSmall!.copyWith(fontWeight: item.isRead ? FontWeight.w500 : FontWeight.w700),
                    ),
                    const SizedBox(height: 2),
                    Text(item.message, style: text.bodyMedium, maxLines: 3, overflow: TextOverflow.ellipsis),
                    const SizedBox(height: AppSpacing.xs),
                    Text(formatDateTime(item.createdAt), style: text.bodySmall),
                  ],
                ),
              ),
              if (appRouteFor(item.link) != null)
                const Padding(
                  padding: EdgeInsets.only(left: AppSpacing.sm, top: 2),
                  child: Icon(Icons.chevron_right, color: AppColors.muted),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
