import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../features/auth/auth.dart';
import '../features/awarding/adjust_evaluation_screen.dart';
import '../features/awarding/awarding.dart';
import '../features/awarding/awarding_detail_screen.dart';
import '../features/auth/login_screen.dart';
import '../features/competitions/competition_detail_screen.dart';
import '../features/exhibitions/exhibitions.dart';
import '../features/exhibitions/staff_entry_screen.dart';
import '../features/exhibitions/staff_exhibition_screens.dart';
import '../features/exhibitions/student_exhibition_screens.dart';
import '../features/grading/grading.dart';
import '../features/grading/grading_detail_screen.dart';
import '../features/grading/grading_list_screen.dart';
import '../features/home/home_screen.dart';
import '../features/staff/staff_shell.dart';
import '../features/student/student_shell.dart';
import '../features/submissions/submission_detail_screen.dart';
import '../features/submissions/submission_form_screen.dart';
import '../features/submissions/submissions.dart';

/// Điều hướng toàn app. Trang /student và /staff chỉ vào được đúng vai trò (US-01 AC4, AC5).
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
          location.startsWith('/student') || location.startsWith('/staff') || location == '/unsupported';

      if (user == null) return needsLogin ? '/login' : null;

      final home = homePathFor(user.role);
      if (location == '/login') return home;
      if (location.startsWith('/student') && user.role != Role.student) return home;
      if (location.startsWith('/staff') && user.role != Role.staff) return home;
      if (location == '/unsupported' && home != '/unsupported') return home;
      return null;
    },
    routes: [
      GoRoute(path: '/', builder: (context, state) => const HomeScreen()),
      GoRoute(
        path: '/competitions/:id',
        builder: (context, state) => CompetitionDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(path: '/login', builder: (context, state) => const LoginScreen()),
      // Khung học viên; `?tab=` chọn tab mở sẵn. Key theo URL để đổi tab khi điều hướng lại bằng go()
      GoRoute(
        path: '/student',
        builder: (context, state) => StudentShell(
          key: ValueKey(state.uri.toString()),
          initialTab: int.tryParse(state.uri.queryParameters['tab'] ?? '') ?? 0,
        ),
      ),
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
      GoRoute(path: '/staff', builder: (context, state) => const StaffShell()),
      // Chấm bài: danh sách bài của cuộc thi và màn chấm một bài (UC-05, SCR-26, SCR-27)
      GoRoute(
        path: '/staff/competitions/:id/submissions',
        builder: (context, state) => GradingListScreen(competitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/submissions/:id/evaluate',
        builder: (context, state) => GradingDetailScreen(id: int.parse(state.pathParameters['id']!)),
      ),
      // Trao giải & công bố (UC-06) và điều chỉnh đánh giá sau công bố (BR-34)
      GoRoute(
        path: '/staff/awarding/:id',
        builder: (context, state) => AwardingDetailScreen(competitionId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/staff/awarding/submissions/:id/adjust',
        builder: (context, state) => AdjustEvaluationScreen(id: int.parse(state.pathParameters['id']!)),
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
