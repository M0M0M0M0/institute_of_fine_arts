import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/async_view.dart';
import '../auth/auth.dart';
import '../competitions/competition_widgets.dart';
import '../competitions/competitions.dart';

/// Trang chủ công khai cho khách (F-M02-01..04).
class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final user = currentUserOf(ref.watch(authControllerProvider));

    return Scaffold(
      appBar: AppBar(
        title: const Text('Institute of Fine Arts'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: user == null
                ? FilledButton.tonal(onPressed: () => context.go('/login'), child: const Text('Đăng nhập'))
                : FilledButton.tonal(
                    onPressed: () => context.go(homePathFor(user.role)),
                    child: const Text('Trang của tôi'),
                  ),
          ),
        ],
      ),
      body: const CompetitionFeed(showWinners: true),
    );
  }
}

/// Đường dẫn trang chính theo vai trò. Admin/Manager dùng bản web (app chỉ phục vụ Student + Staff).
String homePathFor(Role role) => switch (role) {
      Role.student => '/student',
      Role.staff => '/staff',
      _ => '/unsupported',
    };

/// Danh sách cuộc thi đang/sắp diễn ra (+ người đạt giải nếu [showWinners]).
/// Dùng chung cho trang chủ và tab "Cuộc thi" của học viên.
class CompetitionFeed extends ConsumerWidget {
  const CompetitionFeed({super.key, this.showWinners = false});

  final bool showWinners;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(homeProvider);

    return AsyncValueView(
      value: home,
      onRetry: () => ref.invalidate(homeProvider),
      data: (data) => RefreshIndicator(
        onRefresh: () => ref.refresh(homeProvider.future),
        child: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            const SectionTitle('Đang diễn ra'),
            if (data.ongoing.isEmpty) const Text('Hiện chưa có cuộc thi nào đang diễn ra.'),
            for (final c in data.ongoing) _cardOf(context, c),
            const SectionTitle('Sắp diễn ra'),
            if (data.upcoming.isEmpty) const Text('Chưa có cuộc thi sắp tới.'),
            for (final c in data.upcoming) _cardOf(context, c),
            if (showWinners) ...[
              SectionTitle(data.latestGradedCompetition == null
                  ? 'Học viên đạt giải'
                  : 'Đạt giải: ${data.latestGradedCompetition}'),
              if (data.latestWinners.isEmpty) const Text('Chưa có cuộc thi nào công bố kết quả.'),
              for (final w in data.latestWinners)
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: const Icon(Icons.emoji_events),
                  title: Text('${w.studentName} – ${w.awardName}'),
                  subtitle: Text('Tác phẩm: ${w.paintingTitle}'),
                ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _cardOf(BuildContext context, CompetitionSummary c) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: CompetitionCard(competition: c, onTap: () => context.push('/competitions/${c.id}')),
      );
}
