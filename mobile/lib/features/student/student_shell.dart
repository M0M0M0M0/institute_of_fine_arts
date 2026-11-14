import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../auth/auth.dart';
import '../exhibitions/student_exhibition_screens.dart';
import '../home/home_screen.dart';
import '../submissions/my_submissions_screen.dart';
import '../submissions/results_tab.dart';

/// Khung chính của học viên: 3 tab (Cuộc thi, Bài của tôi, Kết quả & giải).
class StudentShell extends ConsumerStatefulWidget {
  const StudentShell({super.key, this.initialTab = 0});

  /// Tab mở sẵn (0..2), lấy từ query `?tab=` của route.
  final int initialTab;

  @override
  ConsumerState<StudentShell> createState() => _StudentShellState();
}

class _StudentShellState extends ConsumerState<StudentShell> {
  late int _tab = widget.initialTab.clamp(0, 3);

  static const _titles = ['Cuộc thi', 'Bài của tôi', 'Kết quả & giải', 'Triển lãm'];

  @override
  Widget build(BuildContext context) {
    final user = currentUserOf(ref.watch(authControllerProvider));

    final body = switch (_tab) {
      0 => const CompetitionFeed(),
      1 => const MySubmissionsTab(),
      2 => const ResultsTab(),
      _ => const StudentExhibitionsTab(),
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
          NavigationDestination(icon: Icon(Icons.palette_outlined), selectedIcon: Icon(Icons.palette), label: 'Cuộc thi'),
          NavigationDestination(icon: Icon(Icons.image_outlined), selectedIcon: Icon(Icons.image), label: 'Bài của tôi'),
          NavigationDestination(icon: Icon(Icons.emoji_events_outlined), selectedIcon: Icon(Icons.emoji_events), label: 'Kết quả'),
          NavigationDestination(icon: Icon(Icons.museum_outlined), selectedIcon: Icon(Icons.museum), label: 'Triển lãm'),
        ],
      ),
    );
  }
}

/// Nút tài khoản: hiện tên + đăng xuất. Dùng chung cho Student và Staff.
class AccountMenu extends ConsumerWidget {
  const AccountMenu({super.key, required this.user});

  final AppUser? user;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return PopupMenuButton<String>(
      icon: const Icon(Icons.account_circle),
      onSelected: (value) {
        if (value == 'logout') ref.read(authControllerProvider.notifier).logout();
      },
      itemBuilder: (context) => [
        PopupMenuItem(enabled: false, child: Text(user?.fullName ?? '')),
        const PopupMenuItem(value: 'logout', child: Text('Đăng xuất')),
      ],
    );
  }
}
