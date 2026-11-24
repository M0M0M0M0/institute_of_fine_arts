import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/account/account_screen.dart';
import '../features/account/unsupported_role_screen.dart';
import '../features/auth/auth.dart';
import '../features/awarding/awarding.dart';
import '../features/awarding/awarding_competitions_screen.dart';
import '../features/awarding/awarding_detail_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/competitions/competition_detail_screen.dart';
import '../features/competitions/competitions_screen.dart';
import '../features/gallery/artwork_screen.dart';
import '../features/gallery/gallery_screen.dart';
import '../features/guest/guest_shell.dart';
import '../features/exhibitions/exhibitions.dart';
import '../features/exhibitions/staff_entry_screen.dart';
import '../features/exhibitions/staff_exhibition_screens.dart';
import '../features/exhibitions/student_exhibition_screens.dart';
import '../features/grading/grading.dart';
import '../features/grading/grading_competitions_screen.dart';
import '../features/grading/grading_detail_screen.dart';
import '../features/grading/grading_list_screen.dart';
import '../features/home/home_screen.dart';
import '../features/staff/dashboard.dart';
import '../features/staff/staff_home_screen.dart';
import '../features/staff/staff_shell.dart';
import '../features/student/dashboard.dart';
import '../features/student/student_competitions_screen.dart';
import '../features/student/student_home_screen.dart';
import '../features/student/student_shell.dart';
import '../features/teacher/teacher_shell.dart';
import '../features/submissions/submission_detail_screen.dart';
import '../features/submissions/submission_form_screen.dart';
import '../features/submissions/my_submissions_screen.dart';
import '../features/submissions/submissions.dart';
import '../shared/auth_image.dart';
import '../shared/tab_scaffold.dart';

/// App navigation. `/student/**` and `/staff/**` are only open to the matching role (US-01 AC4, AC5).
///
/// Tab structure (one `StatefulShellRoute` per role; detail screens are top-level routes, full screen):
/// - guest:   `/` Home, `/competitions`, `/gallery`, "Sign in" (opens `/login`)
/// - student: `/student` Home, `/student/competitions`, `/student/entries`, `/student/exhibitions`, `/student/account`
/// - staff:   `/staff` Home, `/staff/grading`, `/staff/awarding`, `/staff/exhibitions`, `/staff/account`
final routerProvider = Provider<GoRouter>((ref) {
  // Mỗi khi trạng thái đăng nhập đổi, báo go_router chạy lại redirect
  final refresh = ValueNotifier<int>(0);
  ref.listen(authControllerProvider, (prev, next) {
    // Đổi tài khoản (đăng nhập/đăng xuất) → bỏ dữ liệu cache của tài khoản cũ
    if (prev != null && currentUserOf(prev)?.accountId != currentUserOf(next)?.accountId) {
      resetUserScopedData(ref);
      resetGradingData(ref);
      resetExhibitionData(ref);
      resetAwardingData(ref);
      resetAuthImages(ref);
      ref.invalidate(studentDashboardProvider);
      ref.invalidate(staffDashboardProvider);
    }
    refresh.value++;
  });
  ref.onDispose(refresh.dispose);

  return GoRouter(
    initialLocation: '/',
    refreshListenable: refresh,
    redirect: (context, state) {
      final auth = ref.read(authControllerProvider);
      // Đang khôi phục phiên lúc mở app → chưa chuyển trang
      if (auth.isLoading && !auth.hasValue && !auth.hasError) return null;

      final user = currentUserOf(auth);
      final location = state.matchedLocation;
      final needsLogin =
          location.startsWith('/student') ||
          location.startsWith('/staff') ||
          location.startsWith('/teacher') ||
          location == '/unsupported';

      if (user == null) return needsLogin ? '/login' : null;

      // Old links `/student?tab=N` (tabs before the redesign) map to the new tab paths
      if (location == '/student' && state.uri.queryParameters.containsKey('tab')) {
        return switch (state.uri.queryParameters['tab']) {
          '0' => '/student/competitions',
          '1' || '2' => '/student/entries',
          '3' => '/student/exhibitions',
          _ => '/student',
        };
      }

      final home = homePathFor(user.role);
      // Signed-in users start on their role's home, not on the guest home
      if (location == '/login' || location == '/') return home;
      if (location.startsWith('/student') && user.role != Role.student) return home;
      if (location.startsWith('/staff') && user.role != Role.staff) return home;
      if (location.startsWith('/teacher') && user.role != Role.teacher) return home;
      if (location == '/unsupported' && home != '/unsupported') return home;
      return null;
    },
    routes: [
      // Guest shell: Home, Competitions, Gallery (+ "Sign in" button that opens /login)
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => GuestShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/', builder: (context, state) => const HomeScreen())]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/competitions',
              builder: (context, state) => const CompetitionsScreen(),
            ),
          ]),
          StatefulShellBranch(routes: [GoRoute(path: '/gallery', builder: (context, state) => const GalleryScreen())]),
        ],
      ),
      GoRoute(
        path: '/competitions/:id',
        builder: (context, state) => CompetitionDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/gallery/artworks/:id',
        builder: (context, state) => ArtworkScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
      // Student shell: Home, Competitions, My entries, Exhibitions, Account
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => StudentShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(
              routes: [GoRoute(path: '/student', builder: (context, state) => const StudentHomeScreen())]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/student/competitions',
              builder: (context, state) => const StudentCompetitionsScreen(),
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/student/entries',
              builder: (context, state) => const MySubmissionsScreen(),
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/student/exhibitions',
              builder: (context, state) => const TabScaffold(title: 'Exhibitions', body: StudentExhibitionsTab()),
            ),
          ]),
          StatefulShellBranch(
              routes: [GoRoute(path: '/student/account', builder: (context, state) => const AccountScreen())]),
        ],
      ),
      // The old "Results & awards" tab now lives in My entries
      GoRoute(path: '/student/results', redirect: (context, state) => '/student/entries'),
      // Nộp bài mới cho một cuộc thi (phải đặt trước ':id' để "new" không bị hiểu là id)
      GoRoute(
        path: '/student/submissions/new',
        builder: (context, state) => SubmissionFormScreen(
          competitionId: int.parse(state.uri.queryParameters['competitionId'] ?? '0'),
        ),
      ),
      // Chi tiết và sửa bài của học viên
      GoRoute(
        path: '/student/submissions/:id',
        builder: (context, state) => SubmissionDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/student/submissions/:id/edit',
        builder: (context, state) => SubmissionFormScreen(submissionId: int.parse(state.pathParameters['id']!)),
      ),
      // Staff shell: Home, Grading, Awards, Exhibitions, Account
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => StaffShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/staff', builder: (context, state) => const StaffHomeScreen())]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/staff/grading',
              builder: (context, state) => const TabScaffold(title: 'Grading', body: GradingCompetitionsTab()),
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/staff/awarding',
              builder: (context, state) => const TabScaffold(title: 'Awards', body: AwardingCompetitionsTab()),
            ),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/staff/exhibitions',
              builder: (context, state) {
                // ?show=pending|unpaid comes from the staff Home cards; the key rebuilds the tab for a new filter
                final show = state.uri.queryParameters['show'];
                return TabScaffold(title: 'Exhibitions', body: StaffExhibitionsTab(key: ValueKey(show), initialView: show));
              },
            ),
          ]),
          StatefulShellBranch(
              routes: [GoRoute(path: '/staff/account', builder: (context, state) => const AccountScreen())]),
        ],
      ),
      // Teacher shell: Grading (home), Account. Teachers grade the entries of closed competitions.
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) => TeacherShell(navigationShell: shell),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(
              path: '/teacher',
              builder: (context, state) => const TabScaffold(title: 'Grading', body: GradingCompetitionsTab()),
            ),
          ]),
          StatefulShellBranch(
              routes: [GoRoute(path: '/teacher/account', builder: (context, state) => const AccountScreen())]),
        ],
      ),
      GoRoute(
        path: '/teacher/competitions/:id/submissions',
        builder: (context, state) => GradingListScreen(competitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/teacher/submissions/:id/evaluate',
        builder: (context, state) => GradingDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      // Staff: published results, read only (staff do not grade)
      // Chấm bài: danh sách bài của cuộc thi và màn chấm một bài (UC-05, SCR-26, SCR-27)
      GoRoute(
        path: '/staff/competitions/:id/submissions',
        builder: (context, state) => GradingListScreen(competitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/submissions/:id/evaluate',
        builder: (context, state) => GradingDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      // Xếp hạng, công bố và giải do hệ thống trao (UC-06, BR-30)
      GoRoute(
        path: '/staff/awarding/:id',
        builder: (context, state) => AwardingDetailScreen(competitionId: int.parse(state.pathParameters['id']!)),
      ),
      // Triển lãm & bán tranh – học viên (F-M09-03, 04, 10, 12)
      GoRoute(
        path: '/student/exhibitions/:id',
        builder: (context, state) => StudentExhibitionDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/student/exhibitions/:id/propose',
        builder: (context, state) => ProposeEntryScreen(exhibitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/student/exhibition-entries/:id',
        builder: (context, state) => StudentEntryDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      // Triển lãm & bán tranh – Staff (UC-07); "new" đặt trước ':id' để không bị hiểu là id
      GoRoute(path: '/staff/exhibitions/new', builder: (context, state) => const ExhibitionFormScreen()),
      GoRoute(
        path: '/staff/exhibitions/:id',
        builder: (context, state) => StaffExhibitionDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/exhibitions/:id/edit',
        builder: (context, state) => ExhibitionFormScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/exhibitions/:id/add-entry',
        builder: (context, state) => DirectEntryScreen(exhibitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/exhibition-entries/:id',
        builder: (context, state) => StaffEntryDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(path: '/unsupported', builder: (context, state) => const UnsupportedRoleScreen()),
    ],
  );
});
