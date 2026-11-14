import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth.dart';
import '../awarding/awarding_competitions_screen.dart';
import '../exhibitions/staff_exhibition_screens.dart';
import '../grading/grading_competitions_screen.dart';
import '../student/student_shell.dart';

/// Khung chính của giáo viên: 3 tab. Nội dung làm ở bước 4.
class StaffShell extends ConsumerStatefulWidget {
  const StaffShell({super.key});

  @override
  ConsumerState<StaffShell> createState() => _StaffShellState();
}

class _StaffShellState extends ConsumerState<StaffShell> {
  int _tab = 0;

  static const _titles = ['Chấm bài', 'Trao giải', 'Triển lãm'];

  @override
  Widget build(BuildContext context) {
    final user = currentUserOf(ref.watch(authControllerProvider));

    final body = switch (_tab) {
      0 => const GradingCompetitionsTab(),
      1 => const AwardingCompetitionsTab(),
      _ => const StaffExhibitionsTab(),
    };

    return Scaffold(
      appBar: AppBar(
        title: Text(_titles[_tab]),
        actions: [AccountMenu(user: user)],
      ),
      body: body,
      bottomNavigationBar: NavigationBar(
        selectedIndex: _tab,
        onDestinationSelected: (i) => setState(() => _tab = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.rate_review_outlined), selectedIcon: Icon(Icons.rate_review), label: 'Chấm bài'),
          NavigationDestination(icon: Icon(Icons.emoji_events_outlined), selectedIcon: Icon(Icons.emoji_events), label: 'Trao giải'),
          NavigationDestination(icon: Icon(Icons.museum_outlined), selectedIcon: Icon(Icons.museum), label: 'Triển lãm'),
        ],
      ),
    );
  }
}

/// Admin/Manager đăng nhập trên app: hướng dẫn dùng bản web.
class UnsupportedRoleScreen extends ConsumerWidget {
  const UnsupportedRoleScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(title: const Text('Institute of Fine Arts')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.desktop_windows, size: 48),
              const SizedBox(height: 12),
              const Text(
                'Tài khoản Quản trị viên / Quản lý vui lòng dùng phiên bản web.\nỨng dụng di động dành cho Học viên và Giáo viên.',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              FilledButton.tonal(
                onPressed: () => ref.read(authControllerProvider.notifier).logout(),
                child: const Text('Đăng xuất'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
