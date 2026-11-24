import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';

// ---------------- Models (public gallery API, docs/plan/05 section 5) ----------------

/// One public artwork in the gallery. Its images are public:
/// `ApiUrls.submissionThumb(submissionId)` / `ApiUrls.submissionFull(submissionId)`.
class GalleryArtwork {
  const GalleryArtwork({
    required this.submissionId,
    required this.title,
    required this.studentName,
    required this.competitionId,
    required this.competitionTitle,
    this.awardName,
    this.awardRank,
    this.hasThumbnail = false,
    this.competitionDescription,
    this.submittedAt,
  });

  final int submissionId;
  final String title;
  final String studentName;
  final int competitionId;
  final String competitionTitle;

  /// Award of the work (e.g. "First Prize"), null when it won nothing.
  final String? awardName;

  /// Rank of that award (1 first prize, 2 second …), null when it won nothing.
  final int? awardRank;
  final bool hasThumbnail;

  /// Only sent by `GET /api/gallery/artworks/{id}`.
  final String? competitionDescription;
  final DateTime? submittedAt;

  bool get isAwarded => awardName != null && awardName!.isNotEmpty;

  factory GalleryArtwork.fromJson(Map<String, dynamic> json) => GalleryArtwork(
        submissionId: (json['submissionId'] ?? json['id']) as int,
        title: (json['title'] as String?) ?? '',
        studentName: (json['studentName'] as String?) ?? '',
        competitionId: (json['competitionId'] as int?) ?? 0,
        competitionTitle: (json['competitionTitle'] as String?) ?? '',
        awardName: json['awardName'] as String?,
        awardRank: json['awardRank'] as int?,
        hasThumbnail: (json['hasThumbnail'] as bool?) ?? false,
        competitionDescription: json['competitionDescription'] as String?,
        submittedAt: DateTime.tryParse((json['submittedAt'] as String?) ?? ''),
      );
}

/// A page of the gallery: `{ items, page, pageSize, totalCount, totalPages }`.
class GalleryPage {
  const GalleryPage({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
  });

  final List<GalleryArtwork> items;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;

  bool get hasMore => page < totalPages;

  factory GalleryPage.fromJson(Map<String, dynamic> json) {
    final items = ((json['items'] as List?) ?? const [])
        .map((e) => GalleryArtwork.fromJson(e as Map<String, dynamic>))
        .toList();
    return GalleryPage(
      items: items,
      page: (json['page'] as int?) ?? 1,
      pageSize: (json['pageSize'] as int?) ?? items.length,
      totalCount: (json['totalCount'] as int?) ?? items.length,
      totalPages: (json['totalPages'] as int?) ?? 1,
    );
  }
}

/// A gallery filter entry from `GET /api/gallery/categories`; [apiValue] is sent as `category=`.
class GalleryCategory {
  const GalleryCategory({required this.group, required this.key, required this.label});

  final String group;
  final String key;
  final String label;

  /// Value for the `category` query parameter, e.g. `subject/Animals`.
  String get apiValue => '$group/$key';

  factory GalleryCategory.fromJson(Map<String, dynamic> json) => GalleryCategory(
        group: (json['group'] as String?) ?? '',
        key: (json['key'] as String?) ?? '',
        label: (json['label'] as String?) ?? (json['key'] as String?) ?? '',
      );
}

// ---------------- Repository ----------------

class GalleryRepository {
  GalleryRepository(this._dio);

  final Dio _dio;

  /// GET /api/gallery?category=&page=&pageSize=
  Future<GalleryPage> page({String? category, int page = 1, int pageSize = 20}) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/gallery', queryParameters: {
          if (category != null && category.isNotEmpty) 'category': category,
          'page': page,
          'pageSize': pageSize,
        });
        return GalleryPage.fromJson(res.data!);
      });

  /// GET /api/gallery/categories
  Future<List<GalleryCategory>> categories() => guardApi(() async {
        final res = await _dio.get<List<dynamic>>('/api/gallery/categories');
        return res.data!.map((e) => GalleryCategory.fromJson(e as Map<String, dynamic>)).toList();
      });

  /// GET /api/gallery/artworks/{id} (404 when the artwork is not public).
  Future<GalleryArtwork> artwork(int submissionId) => guardApi(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/gallery/artworks/$submissionId');
        return GalleryArtwork.fromJson(res.data!);
      });
}

final galleryRepositoryProvider = Provider<GalleryRepository>((ref) => GalleryRepository(ref.watch(dioProvider)));

/// Gallery filters (`category` null = all).
final galleryCategoriesProvider = FutureProvider<List<GalleryCategory>>(
  (ref) => ref.watch(galleryRepositoryProvider).categories(),
);

/// One public artwork.
final galleryArtworkProvider = FutureProvider.family<GalleryArtwork, int>(
  (ref, id) => ref.watch(galleryRepositoryProvider).artwork(id),
);

/// First page only, handy for a "From the gallery" strip on the home screen.
final galleryPreviewProvider = FutureProvider<GalleryPage>(
  (ref) => ref.watch(galleryRepositoryProvider).page(pageSize: 12),
);

// ---------------- Paged list state (infinite scroll) ----------------

/// State of the gallery list: accumulated artworks, current category and paging flags.
class GalleryListState {
  const GalleryListState({
    this.items = const [],
    this.category,
    this.page = 0,
    this.totalCount = 0,
    this.hasMore = true,
    this.loadingMore = false,
    this.loadMoreError,
  });

  final List<GalleryArtwork> items;
  final String? category;
  final int page;
  final int totalCount;
  final bool hasMore;
  final bool loadingMore;

  /// Message of the last failed "load more", null otherwise.
  final String? loadMoreError;

  GalleryListState copyWith({
    List<GalleryArtwork>? items,
    int? page,
    int? totalCount,
    bool? hasMore,
    bool? loadingMore,
    String? loadMoreError,
    bool clearError = false,
  }) =>
      GalleryListState(
        items: items ?? this.items,
        category: category,
        page: page ?? this.page,
        totalCount: totalCount ?? this.totalCount,
        hasMore: hasMore ?? this.hasMore,
        loadingMore: loadingMore ?? this.loadingMore,
        loadMoreError: clearError ? null : (loadMoreError ?? this.loadMoreError),
      );
}

/// Gallery list for one category (`null` = all) with paging.
/// Watch `galleryListProvider(category)` (AsyncValue); call `.notifier.loadMore()` near the end of the scroll.
final galleryListProvider =
    AsyncNotifierProvider.family<GalleryListNotifier, GalleryListState, String?>(GalleryListNotifier.new);

class GalleryListNotifier extends AsyncNotifier<GalleryListState> {
  GalleryListNotifier(this.category);

  final String? category;
  static const pageSize = 20;

  @override
  Future<GalleryListState> build() async {
    final first = await ref.read(galleryRepositoryProvider).page(category: category, page: 1, pageSize: pageSize);
    return GalleryListState(
      items: first.items,
      category: category,
      page: first.page,
      totalCount: first.totalCount,
      hasMore: first.hasMore,
    );
  }

  /// Appends the next page; ignored while loading or when everything is loaded.
  Future<void> loadMore() async {
    final current = state.value;
    if (current == null || current.loadingMore || !current.hasMore) return;
    state = AsyncData(current.copyWith(loadingMore: true, clearError: true));
    try {
      final next = await ref
          .read(galleryRepositoryProvider)
          .page(category: category, page: current.page + 1, pageSize: pageSize);
      state = AsyncData(current.copyWith(
        items: [...current.items, ...next.items],
        page: next.page,
        totalCount: next.totalCount,
        hasMore: next.hasMore,
        loadingMore: false,
      ));
    } catch (e) {
      state = AsyncData(current.copyWith(loadingMore: false, loadMoreError: ApiException.from(e).message));
    }
  }
}
